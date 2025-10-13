module SideHustleTracking.Domain.Reports

open System
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.UnitsOfMeasure

/// Represents a month (year + month number)
type YearMonth =
    { Year: int
      Month: int }

    member this.ToDate() = DateOnly(this.Year, this.Month, 1)

    /// Add (or subtract) months using the framework's calendar math
    member this.AddMonths(months: int) =
        let d = this.ToDate().AddMonths(months)
        { Year = d.Year; Month = d.Month }

    member this.Previous() = this.AddMonths(-1)
    member this.Next() = this.AddMonths(1)

    static member FromDate(date: DateOnly) =
        { Year = date.Year; Month = date.Month }

    static member Current(today: DateOnly) = YearMonth.FromDate(today)

/// Compare a YearMonth against a date (year+month granularity)
let private compareYearMonthToDate (ym: YearMonth) (date: DateOnly) =
    compare (ym.Year, ym.Month) (date.Year, date.Month)

type DailySummary =
    { Date: DateOnly
      TotalHours: decimal<h>
      TotalCad: decimal<CAD>
      EntryCount: int }

type MonthlySummary =
    { Month: YearMonth
      DailyBreakdown: DailySummary list
      TotalHours: decimal<h>
      TotalCad: decimal<CAD>
      EntryCount: int }

type MonthInYear =
    { YearMonth: YearMonth
      TotalHours: decimal<h>
      TotalCad: decimal<CAD>
      EntryCount: int }

type YearlySummary =
    { Year: int
      MonthlyBreakdown: MonthInYear list // All 12 months (some may be empty)
      TotalHours: decimal<h>
      TotalCad: decimal<CAD>
      EntryCount: int }

/// Keep only closed entries (reports shouldn't include open entries)
let onlyClosedEntries (entries: Entry list) : ClosedInterval list =
    entries
    |> List.choose (function
        | Closed c -> Some c
        | Open _ -> None)

/// Filter closed entries that land in a given YearMonth
let entriesForMonth (ym: YearMonth) (entries: ClosedInterval list) : ClosedInterval list =
    entries
    |> List.filter (fun e -> e.Date.Year = ym.Year && e.Date.Month = ym.Month)

/// Group closed entries by date and aggregate
let aggregateByDate (entries: ClosedInterval list) : DailySummary list =
    entries
    |> List.groupBy _.Date
    |> List.map (fun (date, dayEntries) ->
        let totalHours = dayEntries |> List.sumBy _.Hours
        let totalCad = dayEntries |> List.sumBy _.TotalCad

        { Date = date
          TotalHours = totalHours
          TotalCad = totalCad
          EntryCount = List.length dayEntries })
    |> List.sortBy _.Date // ascending (day 1 -> 31)

/// Create a monthly summary with day-by-day breakdown
let createMonthlySummary (ym: YearMonth) (entries: Entry list) : MonthlySummary =
    let closed = onlyClosedEntries entries
    let monthEntries = entriesForMonth ym closed
    let daily = aggregateByDate monthEntries

    { Month = ym
      DailyBreakdown = daily
      TotalHours = daily |> List.sumBy _.TotalHours
      TotalCad = daily |> List.sumBy _.TotalCad
      EntryCount = daily |> List.sumBy _.EntryCount }

/// Get detailed (closed) entries for a month, newest first (date, then start time)
let getMonthlyEntries (ym: YearMonth) (entries: Entry list) : ClosedInterval list =
    let closed = onlyClosedEntries entries
    entriesForMonth ym closed |> List.sortByDescending (fun e -> e.Date, e.Start)

/// Get all months that have closed entries (newest first)
let getMonthsWithEntries (entries: Entry list) : YearMonth list =
    entries
    |> List.choose (function
        | Closed c -> Some c.Date
        | Open _ -> None)
    |> List.map YearMonth.FromDate
    |> List.distinct
    |> List.sortByDescending (fun ym -> ym.Year, ym.Month)

/// True if the given YearMonth is strictly after the month containing 'today'
let isMonthInFuture (ym: YearMonth) (today: DateOnly) : bool = compareYearMonthToDate ym today > 0

/// Convenience helpers for month navigation (delegate to YearMonth methods)
let previousMonth (ym: YearMonth) : YearMonth = ym.Previous()
let nextMonth (ym: YearMonth) : YearMonth = ym.Next()

// ----------------------
// Yearly aggregation
// ----------------------

/// Create a MonthInYear for a month with no entries
let private emptyMonthInYear (ym: YearMonth) : MonthInYear =
    { YearMonth = ym
      TotalHours = 0m<h>
      TotalCad = 0m<CAD>
      EntryCount = 0 }

/// Group all closed entries once by YearMonth for fast yearly summary
let private groupByYearMonth (entries: Entry list) : Map<YearMonth, ClosedInterval list> =
    entries
    |> onlyClosedEntries
    |> List.groupBy (fun e -> YearMonth.FromDate e.Date)
    |> Map.ofList

/// Create a yearly summary with all 12 months (uses grouped data for O(n) aggregation)
let createYearlySummary (year: int) (entries: Entry list) : YearlySummary =
    let byYm = groupByYearMonth entries

    let monthlyBreakdown =
        [ 1..12 ]
        |> List.map (fun m ->
            let ym = { Year = year; Month = m }

            match Map.tryFind ym byYm with
            | None -> emptyMonthInYear ym
            | Some monthEntries ->
                { YearMonth = ym
                  TotalHours = monthEntries |> List.sumBy _.Hours
                  TotalCad = monthEntries |> List.sumBy _.TotalCad
                  EntryCount = monthEntries.Length })

    { Year = year
      MonthlyBreakdown = monthlyBreakdown
      TotalHours = monthlyBreakdown |> List.sumBy _.TotalHours
      TotalCad = monthlyBreakdown |> List.sumBy _.TotalCad
      EntryCount = monthlyBreakdown |> List.sumBy _.EntryCount }

/// Get all years that have entries (newest first)
let getYearsWithEntries (entries: Entry list) : int list =
    entries
    |> List.choose (function
        | Closed c -> Some c.Date.Year
        | Open _ -> None)
    |> List.distinct
    |> List.sortDescending

/// True if the given year is strictly after the current year
let isYearInFuture (year: int) (today: DateOnly) : bool = year > today.Year
