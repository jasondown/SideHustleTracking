module SideHustleTracking.Views.Reports

open System
open Giraffe.ViewEngine
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.Reports
open SideHustleTracking.Domain.UnitsOfMeasure

// -----------------------------
// Small attribute helpers
// -----------------------------

let private _hx (name: string) (value: string) = attr ("hx-" + name) value
let private _ariaLabel (value: string) = attr "aria-label" value

// -----------------------------
// Formatting helpers
// -----------------------------

let private formatDate (date: DateOnly) = date.ToString("yyyy-MM-dd")
let private formatDateShort (date: DateOnly) = date.ToString("MMM d")
let private formatDay (date: DateOnly) = date.ToString("dddd")
let private formatDecimal (value: decimal) = value.ToString("F2")

let private formatMonth (ym: YearMonth) =
    DateOnly(ym.Year, ym.Month, 1).ToString("MMMM yyyy")

let private formatMonthShort (ym: YearMonth) =
    DateOnly(ym.Year, ym.Month, 1).ToString("MMM yyyy")

// Unified unit stripping + formatting (keeps view logic minimal and consistent)
let private formatHours (h: decimal<h>) : string = formatDecimal (h / 1m<h>)

let private formatCad (c: decimal<CAD>) : string = "$" + formatDecimal (c / 1m<CAD>)

let private formatUsdRate (r: decimal<rate>) : string = "$" + formatDecimal (r / 1m<rate>)

let private formatFxRate (fx: decimal<fx>) : string = formatDecimal (fx / 1m<fx>)

// -----------------------------
// Export section (monthly)
// -----------------------------

let private exportSection (ym: YearMonth) =
    div
        [ _style "margin: 20px 0; padding: 15px; background: #f8f9fa; border-radius: 8px; border: 1px solid #dee2e6;" ]
        [ h3 [ _style "margin: 0 0 15px 0; font-size: 16px; color: #495057;" ] [ str "📊 Export Report" ]

          div
              [ _style "display: flex; gap: 10px; flex-wrap: wrap;" ]
              [
                // Markdown Preview button
                button
                    [ _hx "get" $"/reports/monthly/{ym.Year}/{ym.Month}/export/markdown"
                      _hx "target" "#markdown-preview-content"
                      _hx "swap" "innerHTML"
                      _style
                          "padding: 8px 16px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: 500;"
                      _ariaLabel "Preview Markdown export"
                      attr "onclick" "document.getElementById('markdown-preview').style.display='block';" ]
                    [ str "📄 Preview Markdown" ]

                // Markdown Download button
                a
                    [ _href $"/reports/monthly/{ym.Year}/{ym.Month}/export/markdown?download=true"
                      _style
                          "padding: 8px 16px; background: #28a745; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: 500; text-decoration: none; display: inline-block;"
                      _ariaLabel "Download Markdown file"
                      attr "download" $"time-report-{ym.Year:D4}-{ym.Month:D2}.md" ]
                    [ str "⬇️ Download Markdown" ]

                // CSV Download button
                a
                    [ _href $"/reports/monthly/{ym.Year}/{ym.Month}/export/csv"
                      _style
                          "padding: 8px 16px; background: #17a2b8; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: 500; text-decoration: none; display: inline-block;"
                      _ariaLabel "Download CSV file"
                      attr "download" $"time-entries-{ym.Year:D4}-{ym.Month:D2}.csv" ]
                    [ str "📊 Download CSV" ] ]

          // Preview container (hidden by default)
          div
              [ _id "markdown-preview"
                _style
                    "display: none; margin-top: 15px; padding: 15px; background: white; border: 1px solid #dee2e6; border-radius: 4px; position: relative;" ]
              [ button
                    [ _type "button"
                      _style
                          "position: absolute; top: 10px; right: 10px; background: #dc3545; color: white; border: none; padding: 5px 10px; border-radius: 4px; cursor: pointer; font-weight: bold;"
                      attr "onclick" "document.getElementById('markdown-preview').style.display='none';"
                      _ariaLabel "Close preview" ]
                    [ str "✕ Close" ]

                h4 [ _style "margin: 0 0 10px 0; color: #495057;" ] [ str "Markdown Preview" ]

                p
                    [ _style "font-size: 12px; color: #6c757d; margin-bottom: 10px;" ]
                    [ str "Copy the text below or click 'Copy to Clipboard':" ]

                button
                    [ _type "button"
                      _style
                          "margin-bottom: 10px; padding: 5px 12px; background: #17a2b8; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px;"
                      // modern clipboard API
                      attr "onclick" "navigator.clipboard.writeText(document.getElementById('markdown-text').value)"
                      _ariaLabel "Copy markdown text to clipboard" ]
                    [ str "📋 Copy to Clipboard" ]

                textarea
                    [ _id "markdown-text"
                      _readonly
                      _style
                          "width: 100%; min-height: 400px; font-family: 'Courier New', monospace; font-size: 13px; padding: 10px; border: 1px solid #ced4da; border-radius: 4px; resize: vertical;" ]
                    [ str "" ] // Content loaded by htmx
                |> fun t -> div [ _id "markdown-preview-content" ] [ t ] ] ]

// -----------------------------
// Export section (yearly)
// -----------------------------

let private exportSectionYearly (year: int) =
    div
        [ _style "margin: 20px 0; padding: 15px; background: #f8f9fa; border-radius: 8px; border: 1px solid #dee2e6;" ]
        [ h3 [ _style "margin: 0 0 15px 0; font-size: 16px; color: #495057;" ] [ str "📊 Export Report" ]

          div
              [ _style "display: flex; gap: 10px; flex-wrap: wrap;" ]
              [
                // Markdown Preview button
                button
                    [ _hx "get" $"/reports/yearly/{year}/export/markdown"
                      _hx "target" "#markdown-preview-content-yearly"
                      _hx "swap" "innerHTML"
                      _style
                          "padding: 8px 16px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: 500;"
                      _ariaLabel "Preview Markdown export"
                      attr "onclick" "document.getElementById('markdown-preview-yearly').style.display='block';" ]
                    [ str "📄 Preview Markdown" ]

                // Markdown Download button
                a
                    [ _href $"/reports/yearly/{year}/export/markdown?download=true"
                      _style
                          "padding: 8px 16px; background: #28a745; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: 500; text-decoration: none; display: inline-block;"
                      _ariaLabel "Download Markdown file"
                      attr "download" $"time-report-{year:D4}.md" ]
                    [ str "⬇️ Download Markdown" ]

                // CSV Download button
                a
                    [ _href $"/reports/yearly/{year}/export/csv"
                      _style
                          "padding: 8px 16px; background: #17a2b8; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: 500; text-decoration: none; display: inline-block;"
                      _ariaLabel "Download CSV file"
                      attr "download" $"time-entries-{year:D4}.csv" ]
                    [ str "📊 Download CSV" ] ]

          // Preview container (hidden by default)
          div
              [ _id "markdown-preview-yearly"
                _style
                    "display: none; margin-top: 15px; padding: 15px; background: white; border: 1px solid #dee2e6; border-radius: 4px; position: relative;" ]
              [ button
                    [ _type "button"
                      _style
                          "position: absolute; top: 10px; right: 10px; background: #dc3545; color: white; border: none; padding: 5px 10px; border-radius: 4px; cursor: pointer; font-weight: bold;"
                      attr "onclick" "document.getElementById('markdown-preview-yearly').style.display='none';"
                      _ariaLabel "Close preview" ]
                    [ str "✕ Close" ]

                h4 [ _style "margin: 0 0 10px 0; color: #495057;" ] [ str "Markdown Preview" ]

                p
                    [ _style "font-size: 12px; color: #6c757d; margin-bottom: 10px;" ]
                    [ str "Copy the text below or click 'Copy to Clipboard':" ]

                button
                    [ _type "button"
                      _style
                          "margin-bottom: 10px; padding: 5px 12px; background: #17a2b8; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px;"
                      // modern clipboard API
                      attr
                          "onclick"
                          "navigator.clipboard.writeText(document.getElementById('markdown-text-yearly').value)"
                      _ariaLabel "Copy markdown text to clipboard" ]
                    [ str "📋 Copy to Clipboard" ]

                textarea
                    [ _id "markdown-text-yearly"
                      _readonly
                      _style
                          "width: 100%; min-height: 400px; font-family: 'Courier New', monospace; font-size: 13px; padding: 10px; border: 1px solid #ced4da; border-radius: 4px; resize: vertical;" ]
                    [ str "" ] // Content loaded by htmx
                |> fun t -> div [ _id "markdown-preview-content-yearly" ] [ t ] ] ]

// -----------------------------
// Month navigation controls
// -----------------------------

let private monthNavigation (currentMonth: YearMonth) (today: DateOnly) =
    let prevMonth = currentMonth.Previous()
    let nextMonth = currentMonth.Next()

    let isCurrentMonth =
        currentMonth.Year = today.Year && currentMonth.Month = today.Month

    let isNextMonthFuture = isMonthInFuture nextMonth today

    // Pre-format the labels so the node bodies stay simple
    let prevMonthLabel = "← " + formatMonthShort prevMonth
    let nextMonthLabel = formatMonthShort nextMonth + " →"
    let prevAria = "Go to " + formatMonth prevMonth
    let nextAria = "Go to " + formatMonth nextMonth

    div
        [ _class "month-navigation"
          _style "display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;" ]
        [
          // Previous month button
          button
              [ _hx "get" ("/reports/monthly/" + string prevMonth.Year + "/" + string prevMonth.Month)
                _hx "target" "#report-content"
                _hx "swap" "outerHTML"
                _ariaLabel prevAria
                _style "padding: 8px 16px;" ]
              [ str prevMonthLabel ]

          // Current month indicator
          h2
              [ _style (
                    if isCurrentMonth then
                        "color: #007bff; font-weight: bold;"
                    else
                        ""
                ) ]
              [ str (formatMonth currentMonth)
                if isCurrentMonth then
                    span [ _style "font-size: 14px; margin-left: 10px; color: #28a745;" ] [ str "(Current)" ] ]

          // Next month button (disabled if it would be in future)
          if isNextMonthFuture then
              button
                  [ _disabled
                    _style "padding: 8px 16px; opacity: 0.5; cursor: not-allowed;"
                    _ariaLabel "Next month is in the future" ]
                  [ str "→ Future" ]
          else
              button
                  [ _hx "get" ("/reports/monthly/" + string nextMonth.Year + "/" + string nextMonth.Month)
                    _hx "target" "#report-content"
                    _hx "swap" "outerHTML"
                    _ariaLabel nextAria
                    _style "padding: 8px 16px;" ]
                  [ str nextMonthLabel ] ]

// -----------------------------
// Daily breakdown table (uses domain totals)
// -----------------------------

let private dailyBreakdownTable (summary: MonthlySummary) =
    if List.isEmpty summary.DailyBreakdown then
        p [ _style "color: #666; font-style: italic;" ] [ str "No entries for this month." ]
    else
        table
            [ _class "daily-breakdown"; _style "width: 100%; margin: 20px 0;" ]
            [ thead
                  []
                  [ tr
                        []
                        [ th [ _style "text-align: left;" ] [ str "Date" ]
                          th [ _style "text-align: left;" ] [ str "Day" ]
                          th [ _style "text-align: right;" ] [ str "Entries" ]
                          th [ _style "text-align: right;" ] [ str "Hours" ]
                          th [ _style "text-align: right;" ] [ str "Total (CAD)" ] ] ]
              tbody
                  []
                  [ for daily in summary.DailyBreakdown do
                        tr
                            []
                            [ td [] [ str (formatDateShort daily.Date) ]
                              td [] [ str (formatDay daily.Date) ]
                              td [ _style "text-align: right;" ] [ str (string daily.EntryCount) ]
                              td [ _style "text-align: right;" ] [ str (formatHours daily.TotalHours) ]
                              td [ _style "text-align: right; font-weight: 600;" ] [ str (formatCad daily.TotalCad) ] ] ]
              tfoot
                  [ _style "border-top: 2px solid #333; font-weight: bold;" ]
                  [ tr
                        []
                        [ td [ _colspan "2" ] [ str "Month Total" ]
                          td [ _style "text-align: right;" ] [ str (string summary.EntryCount) ]
                          td [ _style "text-align: right;" ] [ str (formatHours summary.TotalHours) ]
                          td [ _style "text-align: right; color: #28a745;" ] [ str (formatCad summary.TotalCad) ] ] ] ]

// -----------------------------
// Detailed entries (collapsible)
// -----------------------------

let private detailedEntriesSection (entries: ClosedInterval list) (_ym: YearMonth) =
    if List.isEmpty entries then
        div [] []
    else
        let entriesLabel = "View All " + string (List.length entries) + " Entries"

        details
            [ _style "margin-top: 30px; border: 1px solid #ddd; padding: 10px; border-radius: 4px;" ]
            [ summary [ _style "cursor: pointer; font-weight: 600; padding: 5px;" ] [ str entriesLabel ]

              table
                  [ _style "width: 100%; margin-top: 15px;" ]
                  [ thead
                        []
                        [ tr
                              []
                              [ th [] [ str "Date" ]
                                th [] [ str "Start" ]
                                th [] [ str "End" ]
                                th [] [ str "Hours" ]
                                th [] [ str "Rate (USD)" ]
                                th [] [ str "FX Rate" ]
                                th [] [ str "Total (CAD)" ] ] ]
                    tbody
                        []
                        [ for entry in entries do
                              tr
                                  []
                                  [ td [] [ str (formatDateShort entry.Date) ]
                                    td [] [ str (entry.Start.ToString("HH:mm")) ]
                                    td [] [ str (entry.End.ToString("HH:mm")) ]
                                    td [] [ str (formatHours entry.Hours) ]
                                    td [] [ str (formatUsdRate entry.UsdRate) ]
                                    td [] [ str (formatFxRate entry.FxCadPerUsd) ]
                                    td [] [ str (formatCad entry.TotalCad) ] ] ] ] ]

// -----------------------------
// Main views
// -----------------------------

let monthlyReportView (summary: MonthlySummary) (detailedEntries: ClosedInterval list) (today: DateOnly) =
    div
        [ _id "report-content" ]
        [ monthNavigation summary.Month today

          // Link to yearly view for this month's year
          div
              [ _style "text-align: center; margin-bottom: 20px;" ]
              [ a
                    [ _href $"/reports/yearly/{summary.Month.Year}"
                      _style
                          "padding: 8px 16px; background: #17a2b8; color: white; text-decoration: none; border-radius: 4px; display: inline-block;" ]
                    [ str $"↑ View Full Year {summary.Month.Year}" ] ]

          // Export section
          exportSection summary.Month

          // Summary cards
          div
              [ _style "display: flex; gap: 20px; margin-bottom: 30px;" ]
              [ div
                    [ _class "summary-card"
                      _style "flex: 1; padding: 20px; background: #f8f9fa; border-radius: 8px;" ]
                    [ h3 [ _style "margin: 0 0 10px 0; color: #666; font-size: 14px;" ] [ str "Total Hours" ]
                      p
                          [ _style "margin: 0; font-size: 32px; font-weight: bold;" ]
                          [ str (formatHours summary.TotalHours) ] ]

                div
                    [ _class "summary-card"
                      _style "flex: 1; padding: 20px; background: #d4edda; border-radius: 8px;" ]
                    [ h3 [ _style "margin: 0 0 10px 0; color: #666; font-size: 14px;" ] [ str "Total Earned (CAD)" ]
                      p
                          [ _style "margin: 0; font-size: 32px; font-weight: bold; color: #28a745;" ]
                          [ str (formatCad summary.TotalCad) ] ]

                div
                    [ _class "summary-card"
                      _style "flex: 1; padding: 20px; background: #f8f9fa; border-radius: 8px;" ]
                    [ h3 [ _style "margin: 0 0 10px 0; color: #666; font-size: 14px;" ] [ str "Total Entries" ]
                      p [ _style "margin: 0; font-size: 32px; font-weight: bold;" ] [ str (string summary.EntryCount) ] ] ]

          // Daily breakdown
          h3 [] [ str "Daily Breakdown" ]
          dailyBreakdownTable summary

          // Detailed entries (collapsible)
          detailedEntriesSection detailedEntries summary.Month ]

// Full page view for initial load
let monthlyReportPage (summary: MonthlySummary) (detailedEntries: ClosedInterval list) (today: DateOnly) =
    let pageTitle = "Report - " + formatMonth summary.Month

    Layout.layout
        pageTitle
        [ // Navigation bar
          div
              [ _style
                    "display: flex; gap: 10px; margin-bottom: 20px; padding-bottom: 20px; border-bottom: 2px solid #dee2e6;" ]
              [ a
                    [ _href "/"
                      _style
                          "padding: 10px 20px; background: #6c757d; color: white; text-decoration: none; border-radius: 4px;" ]
                    [ str "Time Entries" ]

                a
                    [ _href $"/reports/monthly/%d{summary.Month.Year}/%d{summary.Month.Month}"
                      _style
                          "padding: 10px 20px; background: #007bff; color: white; text-decoration: none; border-radius: 4px; font-weight: 600;" ]
                    [ str "Monthly Report" ]

                a
                    [ _href $"/reports/yearly/%d{summary.Month.Year}"
                      _style
                          "padding: 10px 20px; background: #6c757d; color: white; text-decoration: none; border-radius: 4px;" ]
                    [ str "Yearly Report" ] ]

          h1 [] [ str "Monthly Report" ]

          monthlyReportView summary detailedEntries today ]

// -----------------------------
// Yearly Report Views
// -----------------------------

let private yearNavigation (currentYear: int) (today: DateOnly) =
    let prevYear = currentYear - 1
    let nextYear = currentYear + 1
    let isCurrentYear = currentYear = today.Year
    let isNextYearFuture = isYearInFuture nextYear today

    div
        [ _class "year-navigation"
          _style "display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;" ]
        [
          // Previous year button
          button
              [ _hx "get" ("/reports/yearly/" + string prevYear)
                _hx "target" "#report-content"
                _hx "swap" "outerHTML"
                _ariaLabel ("Go to year " + string prevYear)
                _style "padding: 8px 16px;" ]
              [ str ("← " + string prevYear) ]

          // Current year with link to monthly view
          h2
              [ _style (
                    if isCurrentYear then
                        "color: #007bff; font-weight: bold;"
                    else
                        ""
                ) ]
              [ str (string currentYear)
                if isCurrentYear then
                    span [ _style "font-size: 14px; margin-left: 10px; color: #28a745;" ] [ str "(Current)" ] ]

          // Next year button (disabled if it would be in future)
          if isNextYearFuture then
              button
                  [ _disabled
                    _style "padding: 8px 16px; opacity: 0.5; cursor: not-allowed;"
                    _ariaLabel "Next year is in the future" ]
                  [ str "→ Future" ]
          else
              button
                  [ _hx "get" ("/reports/yearly/" + string nextYear)
                    _hx "target" "#report-content"
                    _hx "swap" "outerHTML"
                    _ariaLabel ("Go to year " + string nextYear)
                    _style "padding: 8px 16px;" ]
                  [ str (string nextYear + " →") ] ]

let private monthlyGrid (summary: YearlySummary) (today: DateOnly) =
    let isCurrentMonth (ym: YearMonth) =
        ym.Year = today.Year && ym.Month = today.Month

    let isFutureMonth (ym: YearMonth) = isMonthInFuture ym today

    div
        [ _style
              "display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 20px; margin: 30px 0;" ]
        [ for month in summary.MonthlyBreakdown do
              let monthName =
                  DateOnly(month.YearMonth.Year, month.YearMonth.Month, 1).ToString("MMMM")

              let bgColor =
                  if month.EntryCount = 0 then "#f8f9fa"
                  elif isCurrentMonth month.YearMonth then "#e3f2fd"
                  else "#fff"

              let borderColor =
                  if isCurrentMonth month.YearMonth then "#007bff"
                  elif month.EntryCount > 0 then "#28a745"
                  else "#dee2e6"

              if isFutureMonth month.YearMonth then
                  // Future month - disabled card
                  div
                      [ _class "month-card"
                        _style
                            "padding: 20px; background: #f8f9fa; border: 2px solid #dee2e6; border-radius: 8px; opacity: 0.5;" ]
                      [ h3 [ _style "margin: 0 0 15px 0; color: #999;" ] [ str monthName ]
                        p [ _style "color: #999; font-style: italic;" ] [ str "Future" ] ]
              elif month.EntryCount = 0 then
                  // Empty month - no link
                  div
                      [ _class "month-card"
                        _style
                            $"padding: 20px; background: %s{bgColor}; border: 2px solid %s{borderColor}; border-radius: 8px;" ]
                      [ h3 [ _style "margin: 0 0 15px 0;" ] [ str monthName ]
                        p [ _style "color: #999; font-style: italic;" ] [ str "No entries" ] ]
              else
                  // Month with data - clickable
                  a
                      [ _href $"/reports/monthly/%d{month.YearMonth.Year}/%d{month.YearMonth.Month}"
                        _class "month-card-link"
                        _style "text-decoration: none; color: inherit; display: block;" ]
                      [ div
                            [ _class "month-card"
                              _style
                                  $"padding: 20px; background: %s{bgColor}; border: 2px solid %s{borderColor}; border-radius: 8px; cursor: pointer; transition: transform 0.2s, box-shadow 0.2s;"
                              attr
                                  "onmouseover"
                                  "this.style.transform='translateY(-2px)'; this.style.boxShadow='0 4px 12px rgba(0,0,0,0.15)';"
                              attr "onmouseout" "this.style.transform=''; this.style.boxShadow='';" ]
                            [ h3
                                  [ _style (
                                        sprintf
                                            "margin: 0 0 15px 0; color: %s;"
                                            (if isCurrentMonth month.YearMonth then "#007bff" else "#333")
                                    ) ]
                                  [ str monthName
                                    if isCurrentMonth month.YearMonth then
                                        span
                                            [ _style "font-size: 12px; margin-left: 8px; color: #28a745;" ]
                                            [ str "(Current)" ] ]

                              div
                                  [ _style "space-y: 8px;" ]
                                  [ div
                                        [ _style "display: flex; justify-content: space-between; margin-bottom: 8px;" ]
                                        [ span [ _style "color: #666;" ] [ str "Hours:" ]
                                          span [ _style "font-weight: 600;" ] [ str (formatHours month.TotalHours) ] ]

                                    div
                                        [ _style "display: flex; justify-content: space-between; margin-bottom: 8px;" ]
                                        [ span [ _style "color: #666;" ] [ str "Earned:" ]
                                          span
                                              [ _style "font-weight: 600; color: #28a745;" ]
                                              [ str (formatCad month.TotalCad) ] ]

                                    div
                                        [ _style "display: flex; justify-content: space-between;" ]
                                        [ span [ _style "color: #666;" ] [ str "Entries:" ]
                                          span [] [ str (string month.EntryCount) ] ] ] ] ] ]

let yearlyReportView (summary: YearlySummary) (today: DateOnly) =
    div
        [ _id "report-content" ]
        [ yearNavigation summary.Year today

          // Export section
          exportSectionYearly summary.Year

          // Year summary cards
          div
              [ _style "display: flex; gap: 20px; margin-bottom: 30px;" ]
              [ div
                    [ _class "summary-card"
                      _style "flex: 1; padding: 20px; background: #f8f9fa; border-radius: 8px;" ]
                    [ h3 [ _style "margin: 0 0 10px 0; color: #666; font-size: 14px;" ] [ str "Total Hours" ]
                      p
                          [ _style "margin: 0; font-size: 32px; font-weight: bold;" ]
                          [ str (formatHours summary.TotalHours) ] ]

                div
                    [ _class "summary-card"
                      _style "flex: 1; padding: 20px; background: #d4edda; border-radius: 8px;" ]
                    [ h3 [ _style "margin: 0 0 10px 0; color: #666; font-size: 14px;" ] [ str "Total Earned (CAD)" ]
                      p
                          [ _style "margin: 0; font-size: 32px; font-weight: bold; color: #28a745;" ]
                          [ str (formatCad summary.TotalCad) ] ]

                div
                    [ _class "summary-card"
                      _style "flex: 1; padding: 20px; background: #f8f9fa; border-radius: 8px;" ]
                    [ h3 [ _style "margin: 0 0 10px 0; color: #666; font-size: 14px;" ] [ str "Total Entries" ]
                      p [ _style "margin: 0; font-size: 32px; font-weight: bold;" ] [ str (string summary.EntryCount) ] ] ]

          // Monthly grid
          h3 [] [ str "Monthly Breakdown" ]
          monthlyGrid summary today ]

// Full page view for yearly report
let yearlyReportPage (summary: YearlySummary) (today: DateOnly) =
    let pageTitle = $"Yearly Report - %d{summary.Year}"

    Layout.layout
        pageTitle
        [
          // Navigation bar
          div
              [ _style
                    "display: flex; gap: 10px; margin-bottom: 20px; padding-bottom: 20px; border-bottom: 2px solid #dee2e6;" ]
              [ a
                    [ _href "/"
                      _style
                          "padding: 10px 20px; background: #6c757d; color: white; text-decoration: none; border-radius: 4px;" ]
                    [ str "Time Entries" ]

                a
                    [ _href $"/reports/monthly/%d{today.Year}/%d{today.Month}"
                      _style
                          "padding: 10px 20px; background: #6c757d; color: white; text-decoration: none; border-radius: 4px;" ]
                    [ str "Monthly Report" ]

                a
                    [ _href $"/reports/yearly/%d{summary.Year}"
                      _style
                          "padding: 10px 20px; background: #007bff; color: white; text-decoration: none; border-radius: 4px; font-weight: 600;" ]
                    [ str "Yearly Report" ] ]

          h1 [] [ str $"Yearly Report - %d{summary.Year}" ]

          yearlyReportView summary today ]
