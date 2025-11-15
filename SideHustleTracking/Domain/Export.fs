module SideHustleTracking.Domain.Export

open System
open System.Globalization
open SideHustleTracking.Domain.Reports
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.UnitsOfMeasure

// -----------------------------
// Markdown Formatting
// -----------------------------

// Use a fixed culture to avoid locale surprises (thousands separators, decimal point)
let private culture = CultureInfo("en-CA")

/// Format a decimal value to 2 decimal places with thousands separators
let private formatDecimal (value: decimal) : string = value.ToString("N2", culture)

/// Format hours (strip unit of measure)
let private formatHours (h: decimal<h>) : string = formatDecimal (h / 1m<h>)

/// Format CAD currency (strip unit of measure and add $)
let private formatCad (cad: decimal<CAD>) : string = "$" + formatDecimal (cad / 1m<CAD>)

/// Format a date as "Jan 2"
let private formatDateShort (date: DateOnly) : string = date.ToString("MMM d", culture)

/// Format day of week
let private formatDay (date: DateOnly) : string = date.ToString("dddd", culture)

/// Format month header (e.g., "January 2025")
let private formatMonthTitle (ym: YearMonth) : string =
    DateOnly(ym.Year, ym.Month, 1).ToString("MMMM yyyy", culture)

/// Format month name only (e.g., "January")
let private formatMonthName (ym: YearMonth) : string =
    DateOnly(ym.Year, ym.Month, 1).ToString("MMMM", culture)

/// Generate Markdown for monthly report (summary-focused, invoice-ready)
let formatMonthlyReportAsMarkdown (summary: MonthlySummary) : string =
    let lines = ResizeArray<string>()

    // Title
    lines.Add($"# Monthly Report - {formatMonthTitle summary.Month}")
    lines.Add("")

    // Summary section
    lines.Add("## Summary")
    lines.Add($"- **Total Hours**: {formatHours summary.TotalHours}")
    lines.Add($"- **Total Earned (CAD)**: {formatCad summary.TotalCad}")
    lines.Add($"- **Total Entries**: {summary.EntryCount}")
    lines.Add("")

    // Daily breakdown table
    lines.Add("## Daily Breakdown")

    if List.isEmpty summary.DailyBreakdown then
        lines.Add("*No entries for this month.*")
    else
        lines.Add("| Date | Day | Hours | Earned (CAD) |")
        // Right-align numeric columns with ---:
        lines.Add("|------|-----|------:|-------------:|")

        for daily in summary.DailyBreakdown do
            let date = formatDateShort daily.Date
            let day = formatDay daily.Date
            let hours = formatHours daily.TotalHours
            let total = formatCad daily.TotalCad
            lines.Add($"| {date} | {day} | {hours} | {total} |")

        // Add separator row (plain dashes for visual separation)
        lines.Add("|------|-----|------|------|")
        lines.Add($"| **Total** | | **{formatHours summary.TotalHours}** | **{formatCad summary.TotalCad}** |")

    // Join all lines with newlines
    String.Join("\n", lines)

/// Generate Markdown for yearly report with all 12 months
let formatYearlyReportAsMarkdown (summary: YearlySummary) : string =
    let lines = ResizeArray<string>()

    // Title
    lines.Add($"# Yearly Report - {summary.Year}")
    lines.Add("")

    // Summary section
    lines.Add("## Annual Summary")
    lines.Add($"- **Total Hours**: {formatHours summary.TotalHours}")
    lines.Add($"- **Total Earned (CAD)**: {formatCad summary.TotalCad}")
    lines.Add($"- **Total Entries**: {summary.EntryCount}")
    lines.Add("")

    // Monthly breakdown table (always show all 12 months for consistency)
    lines.Add("## Monthly Breakdown")
    lines.Add("")
    lines.Add("| Month | Hours | Earned (CAD) | Entries |")
    // Right-align numeric columns
    lines.Add("|-------|------:|-------------:|--------:|")

    for month in summary.MonthlyBreakdown do
        let monthName = formatMonthName month.YearMonth

        if month.EntryCount = 0 then
            lines.Add($"| {monthName} | - | - | - |")
        else
            let hours = formatHours month.TotalHours
            let earned = formatCad month.TotalCad
            let entries = string month.EntryCount
            lines.Add($"| {monthName} | {hours} | {earned} | {entries} |")

    // Add separator row (plain dashes for visual separation)
    lines.Add("|-------|------|------|------|")

    lines.Add(
        $"| **Total** | **{formatHours summary.TotalHours}** | **{formatCad summary.TotalCad}** | **{summary.EntryCount}** |"
    )

    // Join all lines with newlines
    String.Join("\n", lines)

// -----------------------------
// CSV Formatting
// -----------------------------

/// Format a single open entry as a CSV row (no header)
let private formatOpenEntryAsCsvRow (entry: OpenInterval) : string =
    let (EntryId guid) = entry.Id
    let id = guid.ToString()
    let date = entry.Date.ToString("yyyy-MM-dd")
    let start = entry.Start.ToString("HH:mm")

    let usdRate =
        (entry.UsdRate / 1m<rate>).ToString("F2", CultureInfo.InvariantCulture)

    let fxRate =
        (entry.FxCadPerUsd / 1m<fx>).ToString("F4", CultureInfo.InvariantCulture)
    // Open entries have empty end, hours, and total_cad fields
    $"{id},{date},{start},,{usdRate},{fxRate},,"

/// Format a single closed entry as a CSV row (no header)
let private formatClosedEntryAsCsvRow (entry: ClosedInterval) : string =
    let (EntryId guid) = entry.Id
    let id = guid.ToString()
    let date = entry.Date.ToString("yyyy-MM-dd")
    let start = entry.Start.ToString("HH:mm")
    let endTime = entry.End.ToString("HH:mm")
    // Use invariant culture for CSV to ensure consistent decimal format (no thousands separators, period as decimal)
    let usdRate =
        (entry.UsdRate / 1m<rate>).ToString("F2", CultureInfo.InvariantCulture)

    let fxRate =
        (entry.FxCadPerUsd / 1m<fx>).ToString("F4", CultureInfo.InvariantCulture)

    let hours = (entry.Hours / 1m<h>).ToString("F2", CultureInfo.InvariantCulture)

    let totalCad =
        (entry.TotalCad / 1m<CAD>).ToString("F2", CultureInfo.InvariantCulture)

    $"{id},{date},{start},{endTime},{usdRate},{fxRate},{hours},{totalCad}"

/// Generate CSV export for a list of closed entries (includes header)
let formatClosedEntriesAsCsv (entries: ClosedInterval list) : string =
    let header = "id,date,start,end,usd_rate_per_hour,fx_cad_per_usd,hours,total_cad"
    let rows = entries |> List.map formatClosedEntryAsCsvRow
    String.Join("\n", header :: rows)

/// Format a single entry (open or closed) as a CSV row (no header)
let private formatEntryAsCsvRow (entry: Entry) : string =
    match entry with
    | Open o -> formatOpenEntryAsCsvRow o
    | Closed c -> formatClosedEntryAsCsvRow c

/// Generate CSV export for all entries (includes header, supports both open and closed)
let formatAllEntriesAsCsv (entries: Entry list) : string =
    let header = "id,date,start,end,usd_rate_per_hour,fx_cad_per_usd,hours,total_cad"
    let rows = entries |> List.map formatEntryAsCsvRow
    String.Join("\n", header :: rows)
