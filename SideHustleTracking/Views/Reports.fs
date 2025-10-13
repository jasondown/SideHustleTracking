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
        [ div
              [ _style "display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;" ]
              [ h1 [] [ str "Monthly Report" ]
                a
                    [ _href "/"
                      _style
                          "padding: 10px 20px; background: #6c757d; color: white; text-decoration: none; border-radius: 4px;" ]
                    [ str "← Back to Entries" ] ]

          monthlyReportView summary detailedEntries today ]
