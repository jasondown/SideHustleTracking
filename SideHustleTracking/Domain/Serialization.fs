module SideHustleTracking.Domain.Serialization

open System
open SideHustleTracking.Domain.UnitsOfMeasure
open SideHustleTracking.Domain.Types

// CSV row representation (flat structure)
type CsvRow =
    { Id: string
      Date: string
      Start: string
      End: string option
      UsdRatePerHour: decimal
      FxCadPerUsd: decimal
      Hours: decimal option
      TotalCad: decimal option }

// Convert domain Entry to CSV row
let toCsvRow (entry: Entry) : CsvRow =
    match entry with
    | Open o ->
        { Id = let (EntryId guid) = o.Id in guid.ToString()
          Date = o.Date.ToString("yyyy-MM-dd")
          Start = o.Start.ToString("HH:mm")
          End = None
          UsdRatePerHour = o.UsdRate / 1m<rate>
          FxCadPerUsd = o.FxCadPerUsd / 1m<fx>
          Hours = None
          TotalCad = None }
    | Closed c ->
        { Id = let (EntryId guid) = c.Id in guid.ToString()
          Date = c.Date.ToString("yyyy-MM-dd")
          Start = c.Start.ToString("HH:mm")
          End = Some (c.End.ToString("HH:mm"))
          UsdRatePerHour = c.UsdRate / 1m<rate>
          FxCadPerUsd = c.FxCadPerUsd / 1m<fx>
          Hours = Some (c.Hours / 1m<h>)
          TotalCad = Some (c.TotalCad / 1m<CAD>) }

// Convert CSV row to domain Entry
let fromCsvRow (row: CsvRow) : Entry =
    let id = EntryId (Guid.Parse row.Id)
    let date = DateOnly.Parse row.Date
    let start = TimeOnly.Parse row.Start
    let usdRate = row.UsdRatePerHour * 1m<rate>
    let fx = row.FxCadPerUsd * 1m<fx>
    
    match row.End with
    | None ->
        Open { Id = id
               Date = date
               Start = start
               UsdRate = usdRate
               FxCadPerUsd = fx }
    | Some endStr ->
        let endTime = TimeOnly.Parse endStr
        let hours = row.Hours |> Option.defaultValue 0m |> (*) 1m<h>
        let totalCad = row.TotalCad |> Option.defaultValue 0m |> (*) 1m<CAD>
        Closed { Id = id
                 Date = date
                 Start = start
                 End = endTime
                 UsdRate = usdRate
                 FxCadPerUsd = fx
                 Hours = hours
                 TotalCad = totalCad }
