module SideHustleTracking.App

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.ViewEngine
open FSharpPlus.Data
open NodaTime
open SideHustleTracking.Domain.UnitsOfMeasure
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.Calculations
open SideHustleTracking.Domain.Reports
open SideHustleTracking.Domain.Export
open SideHustleTracking.Persistence.Csv
open SideHustleTracking.Services.FxRates
open SideHustleTracking.Views.Entries
open SideHustleTracking.Views.Reports

// Helper to get today's date in America/Toronto timezone
let getTodayInToronto () =
    let torontoZone = DateTimeZoneProviders.Tzdb["America/Toronto"]
    let now = SystemClock.Instance.GetCurrentInstant()
    let zonedNow = now.InZone(torontoZone)
    DateOnly.FromDateTime(zonedNow.ToDateTimeUnspecified())

// Helper to get current date AND time in America/Toronto timezone
let getNowInToronto () : DateOnly * TimeOnly =
    let torontoZone = DateTimeZoneProviders.Tzdb["America/Toronto"]
    let now = SystemClock.Instance.GetCurrentInstant()
    let zonedNow = now.InZone(torontoZone)
    let local = zonedNow.ToDateTimeUnspecified()
    DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local)

let errorMessage (title: string) (errors: string list) =
    div
        [ _id "error-container"
          _class "error"
          _style
              "background-color: #fee; padding: 1rem; margin-bottom: 1rem; border: 1px solid #f00; position: relative;" ]
        [ button
              [ _type "button"
                _style
                    "position: absolute; right: 0.5rem; top: 0.5rem; background: #f00; color: white; border: none; padding: 0.25rem 0.5rem; cursor: pointer;"
                _onclick "document.getElementById('error-container').remove()" ]
              [ str "×" ]
          if not (String.IsNullOrEmpty(title)) then
              h3 [] [ str title ]
          for e in errors do
              p [] [ str e ] ]

// Simple "crosses midnight" helper for DateOnly
let crossesMidnight (startDate: DateOnly) (endDate: DateOnly) = endDate > startDate

let validateNotFuture (date: DateOnly) : Validation<NonEmptyList<string>, DateOnly> =
    let today = getTodayInToronto ()

    if date > today then
        Failure(
            NonEmptyList.singleton (sprintf "Date cannot be in the future (today is %s)" (today.ToString("yyyy-MM-dd")))
        )
    else
        Success date

// Helper to get configured paths
let getCsvPath (ctx: Microsoft.AspNetCore.Http.HttpContext) =
    let config = ctx.GetService<IConfiguration>()
    config["DataPaths:EntriesCsv"]

let getFxSnapshotDir (ctx: Microsoft.AspNetCore.Http.HttpContext) =
    let config = ctx.GetService<IConfiguration>()
    config["DataPaths:FxSnapshotDir"]

let getFxApiUrl (ctx: Microsoft.AspNetCore.Http.HttpContext) =
    let config = ctx.GetService<IConfiguration>()
    config["FxApiUrl"]

// Form binding models
[<CLIMutable>]
type AddEntryForm =
    { date: string
      start: string
      endTime: string // Optional - empty string if not provided
      usdRate: decimal
      fxRate: decimal }

[<CLIMutable>]
type EditEntryForm =
    { id: string
      date: string
      start: string
      endTime: string // Can be empty for open entries
      usdRate: decimal
      fxRate: decimal }

// Validation helpers
let parseDate (dateStr: string) : Validation<NonEmptyList<string>, DateOnly> =
    try
        let parsed = DateOnly.Parse(dateStr)
        // Chain validation: parse then check not future
        Success parsed
    with _ ->
        Failure(NonEmptyList.singleton "Invalid date format")

let parseDateNotFuture (dateStr: string) : Validation<NonEmptyList<string>, DateOnly> =
    parseDate dateStr |> Validation.bind validateNotFuture

let parseTime (timeStr: string) : Validation<NonEmptyList<string>, TimeOnly> =
    try
        Success(TimeOnly.Parse(timeStr))
    with _ ->
        Failure(NonEmptyList.singleton "Invalid time format")

let parseOptionalTime (timeStr: string) : Validation<NonEmptyList<string>, TimeOnly option> =
    if String.IsNullOrWhiteSpace(timeStr) then
        Success None
    else
        parseTime timeStr |> Validation.map Some

let validatePositive (name: string) (value: decimal) : Validation<NonEmptyList<string>, decimal> =
    if value > 0m then
        Success value
    else
        Failure(NonEmptyList.singleton $"%s{name} must be positive")

// Handlers
let indexHandler: HttpHandler =
    fun next ctx ->
        let csvPath = getCsvPath ctx
        let entries = readEntries csvPath
        let view = indexView entries
        htmlView view next ctx

let loadMoreEntriesHandler: HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx
            let allEntries = readEntries csvPath

            // Get count parameter (how many total to show)
            let count =
                ctx.Request.Query.["count"]
                |> Seq.tryHead
                |> Option.bind (fun s ->
                    match Int32.TryParse(s) with
                    | true, v -> Some v
                    | _ -> None)
                |> Option.defaultValue 50

            let view = entriesListViewWithCount allEntries count
            return! htmlView view next ctx
        }

let showAllEntriesHandler: HttpHandler =
    fun next ctx ->
        let csvPath = getCsvPath ctx
        let allEntries = readEntries csvPath
        let view = entriesListViewAll allEntries
        htmlView view next ctx

let addEntryHandler: HttpHandler =
    fun next ctx ->
        task {
            let! form = ctx.BindFormAsync<AddEntryForm>()

            // Validate form data using FSharpPlus Validation
            let validation =
                (fun date start endTimeOpt usdRate fxRate ->
                    // Create an open interval first
                    let openInterval =
                        { Id = EntryId(Guid.NewGuid())
                          Date = date
                          Start = start
                          UsdRate = usdRate * 1m<rate>
                          FxCadPerUsd = fxRate * 1m<fx> }

                    // If end time provided, close it; otherwise return as open
                    match endTimeOpt with
                    | Some endTime ->
                        // Check if end time is "before" start (likely crosses midnight)
                        if endTime < start then
                            Failure(
                                NonEmptyList.singleton
                                    "End time appears to be before start time. For entries crossing midnight, please stop the timer after midnight instead of entering the end time."
                            )
                        else
                            match close openInterval endTime with
                            | Success closed -> Success(Closed closed)
                            | Failure errors -> Failure errors
                    | None -> Success(Open openInterval))
                <!> parseDateNotFuture form.date
                <*> parseTime form.start
                <*> parseOptionalTime form.endTime
                <*> validatePositive "USD Rate" form.usdRate
                <*> validatePositive "FX Rate" form.fxRate

            // Flatten nested validation (from the close operation)
            let flatValidation =
                match validation with
                | Success(Success entry) -> Success entry
                | Success(Failure errors) -> Failure errors
                | Failure errors -> Failure errors

            let csvPath = getCsvPath ctx
            let allEntries = readEntries csvPath

            match flatValidation with
            | Success entry ->
                // Save the entry
                appendEntry csvPath entry

                // Return full entries list view with script to clear form
                let updatedEntries = readEntries csvPath

                let view =
                    div
                        []
                        [ entriesListView updatedEntries
                          script [] [ rawText "document.getElementById('add-entry-form').reset();" ] ]

                return! htmlView view next ctx

            | Failure errors ->
                // Return the full page with dismissible error message at the top
                let errorList = errors |> NonEmptyList.toList

                let viewWithError =
                    div [] [ errorMessage "Error adding entry:" errorList; entriesListView allEntries ]

                return! htmlView viewWithError next ctx
        }

let closeEntryHandler (entryIdStr: string) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx
            let fxApiUrl = getFxApiUrl ctx
            let fxSnapshotDir = getFxSnapshotDir ctx

            match Guid.TryParse(entryIdStr) with
            | false, _ -> return! (setStatusCode 400 >=> text "Invalid entry ID") next ctx
            | true, guid ->
                let entryId = EntryId guid

                match findEntryById csvPath entryId with
                | None -> return! (setStatusCode 404 >=> text "Entry not found") next ctx
                | Some(Closed _) -> return! (setStatusCode 400 >=> text "Entry already closed") next ctx
                | Some(Open openInterval) ->
                    // Current date/time in Toronto
                    let todayDate, currentTime = getNowInToronto ()

                    // If same day: close normally right away
                    if not (crossesMidnight openInterval.Date todayDate) then
                        match close openInterval currentTime with
                        | Success closedInterval ->
                            updateEntry csvPath (Closed closedInterval)
                            let allEntries = readEntries csvPath
                            let view = entriesListView allEntries
                            return! htmlView view next ctx
                        | Failure errors ->
                            let errorList = errors |> NonEmptyList.toList
                            let allEntries = readEntries csvPath

                            let viewWithError =
                                div [] [ errorMessage "Error closing entry:" errorList; entriesListView allEntries ]

                            return! htmlView viewWithError next ctx
                    else
                        // Multi-day safety guard (fast-fail before doing FX I/O)
                        let spanDays = todayDate.DayNumber - openInterval.Date.DayNumber

                        if spanDays > 1 then
                            // Return error with the table still visible
                            let allEntries = readEntries csvPath

                            let message =
                                sprintf
                                    "Cannot close entry from %s. Entry spans %i days. Please close entries daily."
                                    (openInterval.Date.ToString("yyyy-MM-dd"))
                                    spanDays

                            let viewWithError =
                                div [] [ errorMessage "" [ message ]; entriesListView allEntries ]

                            return! htmlView viewWithError next ctx
                        else
                            // Need FX for the new day (today)
                            let! fxRateOpt = lookupRate fxApiUrl fxSnapshotDir todayDate

                            match fxRateOpt with
                            | None ->
                                let allEntries = readEntries csvPath

                                let viewWithError =
                                    div
                                        []
                                        [ errorMessage "Error" [ "Could not fetch FX rate for today" ]
                                          entriesListView allEntries ]

                                return! htmlView viewWithError next ctx
                            | Some fxRate ->
                                // Units-safe rounding/tagging
                                let roundedFx: decimal<fx> = LanguagePrimitives.DecimalWithMeasure(roundRate fxRate)

                                // Split across midnight using domain function
                                match splitCrossMidnight openInterval todayDate currentTime roundedFx with
                                | Failure errors ->
                                    let errorList = errors |> NonEmptyList.toList
                                    let allEntries = readEntries csvPath

                                    let viewWithError =
                                        div
                                            []
                                            [ errorMessage "Error splitting entry:" errorList
                                              entriesListView allEntries ]

                                    return! htmlView viewWithError next ctx

                                | Success closedIntervals ->
                                    match closedIntervals with
                                    | [] ->
                                        return!
                                            (setStatusCode 500 >=> text "Unexpected: no entries from split") next ctx
                                    | firstEntry :: restEntries ->
                                        // Update the original with the first day's closed interval
                                        updateEntry csvPath (Closed firstEntry)

                                        // Append the rest (subsequent days)
                                        for e in restEntries do
                                            appendEntry csvPath (Closed e)

                                        // Return full entries list view with success message
                                        let allEntries = readEntries csvPath

                                        // Create success message
                                        let splitDates =
                                            closedIntervals
                                            |> List.map _.Date.ToString("MMM dd")
                                            |> String.concat " and "

                                        let successMsg =
                                            div
                                                [ _id "success-container"
                                                  _class "success"
                                                  _style
                                                      "background-color: #d4edda; padding: 1rem; margin-bottom: 1rem; border: 1px solid #28a745; position: relative;" ]
                                                [ button
                                                      [ _type "button"
                                                        _style
                                                            "position: absolute; right: 0.5rem; top: 0.5rem; background: #28a745; color: white; border: none; padding: 0.25rem 0.5rem; cursor: pointer;"
                                                        _onclick "document.getElementById('success-container').remove()" ]
                                                      [ str "×" ]
                                                  p
                                                      []
                                                      [ str $"Entry successfully split across midnight ({splitDates})." ]
                                                  small
                                                      [ _style "color: #666;" ]
                                                      [ str
                                                            "Note: Cross-midnight splits may lose up to 1 minute of tracked time." ] ]

                                        let view = div [] [ successMsg; entriesListView allEntries ]
                                        return! htmlView view next ctx
        }

let showEditFormHandler (entryIdStr: string) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx

            match Guid.TryParse(entryIdStr) with
            | false, _ -> return! (setStatusCode 400 >=> text "Invalid entry ID") next ctx
            | true, guid ->
                let entryId = EntryId guid

                match findEntryById csvPath entryId with
                | None -> return! (setStatusCode 404 >=> text "Entry not found") next ctx
                | Some entry ->
                    let formRow = entryEditFormRow entry
                    return! htmlView formRow next ctx
        }

let cancelEditHandler (entryIdStr: string) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx

            match Guid.TryParse(entryIdStr) with
            | false, _ -> return! (setStatusCode 400 >=> text "Invalid entry ID") next ctx
            | true, guid ->
                let entryId = EntryId guid

                match findEntryById csvPath entryId with
                | None -> return! (setStatusCode 404 >=> text "Entry not found") next ctx
                | Some entry ->
                    let row = entryRow entry
                    return! htmlView row next ctx
        }

let updateEntryHandler: HttpHandler =
    fun next ctx ->
        task {
            let! form = ctx.BindFormAsync<EditEntryForm>()
            let csvPath = getCsvPath ctx

            match Guid.TryParse(form.id) with
            | false, _ -> return! (setStatusCode 400 >=> text "Invalid entry ID") next ctx
            | true, guid ->
                let entryId = EntryId guid

                // Validate form data
                let validation =
                    (fun date start endTimeOpt usdRate fxRate ->
                        // Create an open interval first
                        let openInterval =
                            { Id = entryId // Keep the same ID
                              Date = date
                              Start = start
                              UsdRate = usdRate * 1m<rate>
                              FxCadPerUsd = fxRate * 1m<fx> }

                        // If end time provided, close it; otherwise return as open
                        match endTimeOpt with
                        | Some endTime ->
                            match close openInterval endTime with
                            | Success closed -> Success(Closed closed)
                            | Failure errors -> Failure errors
                        | None -> Success(Open openInterval))
                    <!> parseDateNotFuture form.date
                    <*> parseTime form.start
                    <*> parseOptionalTime form.endTime
                    <*> validatePositive "USD Rate" form.usdRate
                    <*> validatePositive "FX Rate" form.fxRate

                // Flatten nested validation
                let flatValidation =
                    match validation with
                    | Success(Success entry) -> Success entry
                    | Success(Failure errors) -> Failure errors
                    | Failure errors -> Failure errors

                match flatValidation with
                | Success entry ->
                    // Update the entry
                    updateEntry csvPath entry

                    // Return full entries list view
                    let allEntries = readEntries csvPath
                    let view = entriesListView allEntries
                    return! htmlView view next ctx

                | Failure errors ->
                    // Return full table with error message at top
                    let errorList = errors |> NonEmptyList.toList
                    let allEntries = readEntries csvPath

                    let viewWithError =
                        div [] [ errorMessage "Error updating entry:" errorList; entriesListView allEntries ]

                    return! htmlView viewWithError next ctx
        }

let showDeleteConfirmHandler (entryIdStr: string) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx

            match Guid.TryParse(entryIdStr) with
            | false, _ -> return! (setStatusCode 400 >=> text "Invalid entry ID") next ctx
            | true, guid ->
                let entryId = EntryId guid

                match findEntryById csvPath entryId with
                | None -> return! (setStatusCode 404 >=> text "Entry not found") next ctx
                | Some entry ->
                    let confirmRow = entryDeleteConfirmRow entry
                    return! htmlView confirmRow next ctx
        }

let deleteEntryHandler (entryIdStr: string) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx

            match Guid.TryParse(entryIdStr) with
            | false, _ -> return! (setStatusCode 400 >=> text "Invalid entry ID") next ctx
            | true, guid ->
                let entryId = EntryId guid

                // Delete the entry
                deleteEntry csvPath entryId

                // Return full entries list view (handles empty state)
                let allEntries = readEntries csvPath
                let view = entriesListView allEntries
                return! htmlView view next ctx
        }

let getFxRateHandler (dateStr: string) : HttpHandler =
    fun next ctx ->
        task {
            let apiBaseUrl = getFxApiUrl ctx
            let fxSnapshotDir = getFxSnapshotDir ctx

            match DateOnly.TryParse(dateStr) with
            | false, _ -> return! (setStatusCode 400 >=> text "Invalid date format") next ctx
            | true, date ->
                let today = getTodayInToronto ()

                if date > today then
                    return! (setStatusCode 400 >=> text "Cannot fetch rates for future dates") next ctx
                else
                    let! rateOpt = lookupRate apiBaseUrl fxSnapshotDir date

                    match rateOpt with
                    | Some rate ->
                        let rounded = roundRate rate
                        return! text (string rounded) next ctx
                    | None -> return! (setStatusCode 500 >=> text "Could not fetch FX rate") next ctx
        }

let exportAllEntriesHandler: HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx
            let allEntries = readEntries csvPath

            // Generate CSV content (includes both open and closed entries)
            let csv = formatAllEntriesAsCsv allEntries

            // Generate filename with current timestamp for uniqueness
            let now = DateTime.Now
            let timestamp = now.ToString("yyyy-MM-dd-HHmmss")
            let filename = $"time-entries-all-{timestamp}.csv"

            return!
                (setHttpHeader "Content-Type" "text/csv; charset=utf-8"
                 >=> setHttpHeader "Content-Disposition" $"attachment; filename=\"{filename}\""
                 >=> setBodyFromString csv)
                    next
                    ctx
        }

let monthlyReportHandler (year: int, month: int) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx
            let today = getTodayInToronto ()

            // Validate the year/month
            if month < 1 || month > 12 then
                return! (setStatusCode 400 >=> text "Invalid month") next ctx
            else
                let yearMonth = { Year = year; Month = month }

                // Check if month is in future
                if isMonthInFuture yearMonth today then
                    return! (setStatusCode 400 >=> text "Cannot view future months") next ctx
                else
                    // Load all entries and create report
                    let allEntries = readEntries csvPath
                    let summary = createMonthlySummary yearMonth allEntries
                    let detailedEntries = getMonthlyEntries yearMonth allEntries

                    // Check if this is a htmx request (partial update) or full page load
                    let isHtmxRequest = ctx.Request.Headers.ContainsKey("HX-Request")

                    let view =
                        if isHtmxRequest then
                            // Return just the report content div for htmx swap
                            monthlyReportView summary detailedEntries today
                        else
                            // Return full page for direct navigation
                            monthlyReportPage summary detailedEntries today

                    return! htmlView view next ctx
        }

let monthlyReportMarkdownHandler (year: int, month: int) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx
            let today = getTodayInToronto ()

            // Validate the year/month
            if month < 1 || month > 12 then
                return! (setStatusCode 400 >=> text "Invalid month") next ctx
            else
                let yearMonth = { Year = year; Month = month }

                // Check if month is in future
                if isMonthInFuture yearMonth today then
                    return! (setStatusCode 400 >=> text "Cannot export future months") next ctx
                else
                    // Load all entries and create report
                    let allEntries = readEntries csvPath
                    let summary = createMonthlySummary yearMonth allEntries

                    // Generate Markdown content
                    let markdown = formatMonthlyReportAsMarkdown summary

                    // Check if this is a download request or preview (htmx)
                    let isDownload = ctx.Request.Query.ContainsKey("download")
                    let isHtmxRequest = ctx.Request.Headers.ContainsKey("HX-Request")

                    if isHtmxRequest then
                        // For htmx preview, return just the Markdown text wrapped in textarea
                        let previewHtml =
                            textarea
                                [ _id "markdown-text"
                                  _readonly
                                  _style
                                      "width: 100%; min-height: 400px; font-family: 'Courier New', monospace; font-size: 13px; padding: 10px; border: 1px solid #ced4da; border-radius: 4px; resize: vertical;" ]
                                [ str markdown ]

                        return! htmlView previewHtml next ctx
                    else
                        // Regular request - either download or inline view
                        let filename = $"time-report-%04d{year}-%02d{month}.md"

                        let disposition =
                            if isDownload then
                                $"attachment; filename=\"{filename}\""
                            else
                                $"inline; filename=\"{filename}\""

                        return!
                            (setHttpHeader "Content-Type" "text/markdown; charset=utf-8"
                             >=> setHttpHeader "Content-Disposition" disposition
                             >=> setBodyFromString markdown)
                                next
                                ctx
        }

let monthlyReportCsvHandler (year: int, month: int) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx
            let today = getTodayInToronto ()

            // Validate the year/month
            if month < 1 || month > 12 then
                return! (setStatusCode 400 >=> text "Invalid month") next ctx
            else
                let yearMonth = { Year = year; Month = month }

                // Check if month is in future
                if isMonthInFuture yearMonth today then
                    return! (setStatusCode 400 >=> text "Cannot export future months") next ctx
                else
                    // Load all entries and get closed entries for this month
                    let allEntries = readEntries csvPath
                    let closedEntries = getMonthlyEntries yearMonth allEntries

                    // Generate CSV content
                    let csv = formatClosedEntriesAsCsv closedEntries

                    // Set filename and headers for download
                    let filename = $"time-entries-{year:D4}-{month:D2}.csv"

                    return!
                        (setHttpHeader "Content-Type" "text/csv; charset=utf-8"
                         >=> setHttpHeader "Content-Disposition" $"attachment; filename=\"{filename}\""
                         >=> setBodyFromString csv)
                            next
                            ctx
        }

let yearlyReportHandler (year: int) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx
            let today = getTodayInToronto ()

            // Check if year is in future
            if isYearInFuture year today then
                return! (setStatusCode 400 >=> text "Cannot view future years") next ctx
            else
                // Load all entries and create yearly report
                let allEntries = readEntries csvPath
                let summary = createYearlySummary year allEntries

                // Check if this is an htmx request (partial update) or full page load
                let isHtmxRequest = ctx.Request.Headers.ContainsKey("HX-Request")

                let view =
                    if isHtmxRequest then
                        // Return just the report content div for htmx swap
                        yearlyReportView summary today
                    else
                        // Return full page for direct navigation
                        yearlyReportPage summary today

                return! htmlView view next ctx
        }

let yearlyReportMarkdownHandler (year: int) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx
            let today = getTodayInToronto ()

            // Check if year is in future
            if isYearInFuture year today then
                return! (setStatusCode 400 >=> text "Cannot export future years") next ctx
            else
                // Load all entries and create yearly report
                let allEntries = readEntries csvPath
                let summary = createYearlySummary year allEntries

                // Generate Markdown content
                let markdown = formatYearlyReportAsMarkdown summary

                // Check if this is a download request or preview (htmx)
                let isDownload = ctx.Request.Query.ContainsKey("download")
                let isHtmxRequest = ctx.Request.Headers.ContainsKey("HX-Request")

                if isHtmxRequest then
                    // For htmx preview, return just the Markdown text wrapped in textarea
                    let previewHtml =
                        textarea
                            [ _id "markdown-text"
                              _readonly
                              _style
                                  "width: 100%; min-height: 400px; font-family: 'Courier New', monospace; font-size: 13px; padding: 10px; border: 1px solid #ced4da; border-radius: 4px; resize: vertical;" ]
                            [ str markdown ]

                    return! htmlView previewHtml next ctx
                else
                    // Regular request - either download or inline view
                    let filename = $"time-report-%04d{year}.md"

                    let disposition =
                        if isDownload then
                            $"attachment; filename=\"{filename}\""
                        else
                            $"inline; filename=\"{filename}\""

                    return!
                        (setHttpHeader "Content-Type" "text/markdown; charset=utf-8"
                         >=> setHttpHeader "Content-Disposition" disposition
                         >=> setBodyFromString markdown)
                            next
                            ctx
        }

let yearlyReportCsvHandler (year: int) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx
            let today = getTodayInToronto ()

            // Check if year is in future
            if isYearInFuture year today then
                return! (setStatusCode 400 >=> text "Cannot export future years") next ctx
            else
                // Load all entries and filter to closed entries for this year
                let allEntries = readEntries csvPath

                let closedEntries =
                    allEntries
                    |> List.choose (function
                        | Closed c when c.Date.Year = year -> Some c
                        | _ -> None)
                    |> List.sortBy (fun c -> c.Date, c.Start)

                // Generate CSV content
                let csv = formatClosedEntriesAsCsv closedEntries

                // Set filename and headers for download
                let filename = $"time-entries-{year:D4}.csv"

                return!
                    (setHttpHeader "Content-Type" "text/csv; charset=utf-8"
                     >=> setHttpHeader "Content-Disposition" $"attachment; filename=\"{filename}\""
                     >=> setBodyFromString csv)
                        next
                        ctx
        }

let webApp =
    choose
        [ GET >=> route "/" >=> indexHandler
          GET >=> routef "/reports/monthly/%i/%i" monthlyReportHandler
          GET
          >=> routef "/reports/monthly/%i/%i/export/markdown" monthlyReportMarkdownHandler
          GET >=> routef "/reports/monthly/%i/%i/export/csv" monthlyReportCsvHandler
          GET >=> routef "/reports/yearly/%i" yearlyReportHandler
          GET >=> routef "/reports/yearly/%i/export/markdown" yearlyReportMarkdownHandler
          GET >=> routef "/reports/yearly/%i/export/csv" yearlyReportCsvHandler
          GET >=> routef "/fx/%s" getFxRateHandler
          GET >=> route "/entries/export/csv" >=> exportAllEntriesHandler
          GET >=> route "/entries/more" >=> loadMoreEntriesHandler
          GET >=> route "/entries/all" >=> showAllEntriesHandler
          POST >=> route "/entries" >=> addEntryHandler
          POST >=> routef "/entries/%s/close" closeEntryHandler
          GET >=> routef "/entries/%s/edit" showEditFormHandler
          GET >=> routef "/entries/%s/cancel" cancelEditHandler
          POST >=> route "/entries/update" >=> updateEntryHandler
          GET >=> routef "/entries/%s/delete/confirm" showDeleteConfirmHandler
          POST >=> routef "/entries/%s/delete" deleteEntryHandler
          setStatusCode 404 >=> text "Not Found" ]

// Configuration
let configureApp (app: IApplicationBuilder) = app.UseStaticFiles().UseGiraffe webApp

let configureServices (services: IServiceCollection) = services.AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder.Configure(configureApp).ConfigureServices(configureServices)
            |> ignore)
        .Build()
        .Run()

    0
