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
        Failure (NonEmptyList.singleton "End time must be after start time")
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
