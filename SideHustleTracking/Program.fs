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
open SideHustleTracking.Domain.UnitsOfMeasure
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.Calculations
open SideHustleTracking.Persistence.Csv
open SideHustleTracking.Views.Entries
open SideHustleTracking.Services.FxRates

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
        Success(DateOnly.Parse(dateStr))
    with _ ->
        Failure(NonEmptyList.singleton "Invalid date format")

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
                        match close openInterval endTime with
                        | Success closed -> Success(Closed closed)
                        | Failure errors -> Failure errors
                    | None -> Success(Open openInterval))
                <!> parseDate form.date
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

            match flatValidation with
            | Success entry ->
                // Save the entry
                let csvPath = getCsvPath ctx
                appendEntry csvPath entry

                // Return full entries list view for consistency
                let allEntries = readEntries csvPath
                let view = entriesListView allEntries
                return! htmlView view next ctx

            | Failure errors ->
                // Return error message for htmx
                let errorList = errors |> NonEmptyList.toList

                let errorView =
                    div
                        [ _class "error" ]
                        [ for e in errorList do
                              p [] [ str e ] ]

                return! htmlView errorView next ctx
        }

let closeEntryHandler (entryIdStr: string) : HttpHandler =
    fun next ctx ->
        task {
            let csvPath = getCsvPath ctx

            match Guid.TryParse(entryIdStr) with
            | false, _ -> return! (setStatusCode 400 >=> text "Invalid entry ID") next ctx
            | true, guid ->
                let entryId = EntryId guid

                match findEntryById csvPath entryId with
                | None -> return! (setStatusCode 404 >=> text "Entry not found") next ctx
                | Some(Closed _) -> return! (setStatusCode 400 >=> text "Entry already closed") next ctx
                | Some(Open openInterval) ->
                    // Close with current time
                    let now = TimeOnly.FromDateTime(DateTime.Now)

                    match close openInterval now with
                    | Success closedInterval ->
                        let closedEntry = Closed closedInterval
                        updateEntry csvPath closedEntry

                        // Return full entries list view (handles empty state)
                        let allEntries = readEntries csvPath
                        let view = entriesListView allEntries
                        return! htmlView view next ctx
                    | Failure errors ->
                        let errorList = errors |> NonEmptyList.toList
                        let errorMsg = String.concat "; " errorList
                        return! (setStatusCode 400 >=> text errorMsg) next ctx
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
                    <!> parseDate form.date
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
                    let csvPath = getCsvPath ctx
                    updateEntry csvPath entry

                    // Return full entries list view (handles empty state)
                    let allEntries = readEntries csvPath
                    let view = entriesListView allEntries
                    return! htmlView view next ctx

                | Failure errors ->
                    // Return error in the row
                    let errorList = errors |> NonEmptyList.toList

                    let errorView =
                        tr
                            []
                            [ td
                                  [ _colspan "8"; _class "error" ]
                                  [ for e in errorList do
                                        p [] [ str e ] ] ]

                    return! htmlView errorView next ctx
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
                let! rateOpt = lookupRate apiBaseUrl fxSnapshotDir date

                match rateOpt with
                | Some rate ->
                    let rounded = roundRate rate
                    return! text (string rounded) next ctx
                | None -> return! (setStatusCode 500 >=> text "Could not fetch FX rate") next ctx
        }

let webApp =
    choose
        [ GET >=> route "/" >=> indexHandler
          GET >=> routef "/fx/%s" getFxRateHandler
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
