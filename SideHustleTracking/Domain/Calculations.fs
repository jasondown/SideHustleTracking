module SideHustleTracking.Domain.Calculations

open FSharpPlus.Data
open SideHustleTracking.Domain.UnitsOfMeasure
open SideHustleTracking.Domain.Types

/// Calculate duration in hours between start and end times
let durationHours (start: LocalTime) (endTime: LocalTime) : decimal<h> =
    let duration = endTime - start
    decimal duration.TotalHours * 1m<h>

/// Calculate USD total from rate and hours
let usdTotal (usdRate: decimal<rate>) (hours: decimal<h>) : decimal<USD> =
    (usdRate / 1m<rate>) * (hours / 1m<h>) * 1m<USD>

/// Convert USD to CAD using exchange rate
let cadTotal (fx: decimal<fx>) (usd: decimal<USD>) : decimal<CAD> =
    (fx / 1m<fx>) * (usd / 1m<USD>) * 1m<CAD>

/// Close an open interval with validation
let close (openInterval: OpenInterval) (endTime: LocalTime) : Validation<NonEmptyList<string>, ClosedInterval> =
    if endTime <= openInterval.Start then
        Failure(NonEmptyList.singleton "End time must be after start time")
    else
        let hours = durationHours openInterval.Start endTime
        let usdAmount = usdTotal openInterval.UsdRate hours
        let cadAmount = cadTotal openInterval.FxCadPerUsd usdAmount

        let closedInterval =
            { Id = openInterval.Id
              Date = openInterval.Date
              Start = openInterval.Start
              End = endTime
              UsdRate = openInterval.UsdRate
              FxCadPerUsd = openInterval.FxCadPerUsd
              Hours = hours
              TotalCad = cadAmount }

        Success closedInterval

/// Check if a time interval crosses midnight
let crossesMidnight (date: LocalDate) (endDate: LocalDate) : bool = endDate > date

/// Calculate hours from start time until midnight (23:59:59.999...)
let hoursUntilMidnight (start: LocalTime) : decimal<h> =
    let endOfDay = LocalTime.MaxValue // This is 23:59:59.9999999
    durationHours start endOfDay

/// Calculate hours from midnight (00:00:00) until end time
let hoursFromMidnight (endTime: LocalTime) : decimal<h> =
    let startOfDay = LocalTime.MinValue // This is 00:00:00
    durationHours startOfDay endTime

/// Split an open interval that crosses midnight into closed intervals for each day
let splitCrossMidnight
    (openInterval: OpenInterval)
    (endDate: LocalDate)
    (endTime: LocalTime)
    (fxForNewDay: decimal<fx>)
    : Validation<NonEmptyList<string>, ClosedInterval list> =

    if endDate < openInterval.Date then
        Failure(NonEmptyList.singleton "End date cannot be before start date")
    elif endDate = openInterval.Date then
        // Same day - just close normally
        close openInterval endTime |> Validation.map List.singleton
    else
        // Crosses midnight - split into multiple entries
        let rec splitDays (currentDate: LocalDate) (startTime: LocalTime) (accum: ClosedInterval list) =
            if currentDate = endDate then
                // Last day - create entry from midnight to end time
                let hours = hoursFromMidnight endTime
                let usdAmount = usdTotal openInterval.UsdRate hours
                let cadAmount = cadTotal fxForNewDay usdAmount

                let lastEntry =
                    { Id = EntryId(System.Guid.NewGuid())
                      Date = currentDate
                      Start = LocalTime.MinValue
                      End = endTime
                      UsdRate = openInterval.UsdRate
                      FxCadPerUsd = fxForNewDay
                      Hours = hours
                      TotalCad = cadAmount }

                Success(List.rev (lastEntry :: accum))
            elif currentDate = openInterval.Date then
                // First day - create entry from start to midnight
                let hours = hoursUntilMidnight startTime
                let usdAmount = usdTotal openInterval.UsdRate hours
                let cadAmount = cadTotal openInterval.FxCadPerUsd usdAmount

                let firstEntry =
                    { Id = openInterval.Id // Keep original ID for first entry
                      Date = currentDate
                      Start = startTime
                      End = LocalTime.MaxValue
                      UsdRate = openInterval.UsdRate
                      FxCadPerUsd = openInterval.FxCadPerUsd
                      Hours = hours
                      TotalCad = cadAmount }

                splitDays (currentDate.AddDays(1)) LocalTime.MinValue (firstEntry :: accum)
            else
                // Middle days - full 24-hour entries (if we support multi-day splits)
                // For now, we'll return an error if gap is more than 1 day
                Failure(NonEmptyList.singleton "Entry spans multiple days. Please close entries daily.")

        splitDays openInterval.Date openInterval.Start []
