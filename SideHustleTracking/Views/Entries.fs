module SideHustleTracking.Views.Entries

open System
open Giraffe.ViewEngine
open SideHustleTracking.Domain.Types
open SideHustleTracking.Domain.UnitsOfMeasure

// Helper to create htmx attributes
let private _hx (name: string) (value: string) = attr ("hx-" + name) value
let private _ariaLabel (value: string) = attr "aria-label" value

let private formatTime (time: TimeOnly) = time.ToString("HH:mm")
let private formatDate (date: DateOnly) = date.ToString("yyyy-MM-dd")
let private formatDecimal (value: decimal) = value.ToString("F2")

// Helper to strip units for display (divide by 1 with the unit)
let private stripRate (r: decimal<rate>) : decimal = r / 1m<rate>
let private stripFx (fx: decimal<fx>) : decimal = fx / 1m<fx>
let private stripHours (h: decimal<h>) : decimal = h / 1m<h>
let private stripCad (c: decimal<CAD>) : decimal = c / 1m<CAD>
let private stripUsd (u: decimal<USD>) : decimal = u / 1m<USD>

let entryRow (entry: Entry) =
    match entry with
    | Open o ->
        let (EntryId guid) = o.Id

        tr
            [ _class "open-entry" ]
            [ td [] [ str (formatDate o.Date) ]
              td [] [ str (formatTime o.Start) ]
              td [] [ str "-" ]
              td [] [ str $"$%s{formatDecimal (stripRate o.UsdRate)}" ]
              td [] [ str (formatDecimal (stripFx o.FxCadPerUsd)) ]
              td [] [ str "-" ]
              td [] [ str "-" ]
              td
                  []
                  [ button
                        [ _hx "post" $"/entries/{guid.ToString()}/close"
                          _hx "target" "#entries-list" // Changed from #entries-tbody
                          _hx "swap" "outerHTML"
                          _style "margin-right: 5px;" ]
                        [ str "Stop" ]
                    button
                        [ _hx "get" $"/entries/%s{guid.ToString()}/edit"
                          _hx "target" "closest tr"
                          _hx "swap" "outerHTML"
                          _style "margin-right: 5px;" ]
                        [ str "Edit" ]
                    button
                        [ _hx "get" $"/entries/%s{guid.ToString()}/delete/confirm"
                          _hx "target" "closest tr"
                          _hx "swap" "outerHTML"
                          _style "background: #dc3545;" ]
                        [ str "Delete" ] ] ]
    | Closed c ->
        let (EntryId guid) = c.Id

        tr
            [ _class "closed-entry" ]
            [ td [] [ str (formatDate c.Date) ]
              td [] [ str (formatTime c.Start) ]
              td [] [ str (formatTime c.End) ]
              td [] [ str $"$%s{formatDecimal (stripRate c.UsdRate)}" ]
              td [] [ str (formatDecimal (stripFx c.FxCadPerUsd)) ]
              td [] [ str (formatDecimal (stripHours c.Hours)) ]
              td [] [ str $"$%s{formatDecimal (stripCad c.TotalCad)}" ]
              td
                  []
                  [ button
                        [ _hx "get" $"/entries/%s{guid.ToString()}/edit"
                          _hx "target" "closest tr"
                          _hx "swap" "outerHTML"
                          _style "margin-right: 5px;" ]
                        [ str "Edit" ]
                    button
                        [ _hx "get" $"/entries/%s{guid.ToString()}/delete/confirm"
                          _hx "target" "closest tr"
                          _hx "swap" "outerHTML"
                          _style "background: #dc3545;" ]
                        [ str "Delete" ] ] ]

let entryEditFormRow (entry: Entry) =

    let now = DateTime.Now

    let entryId, date, start, endTimeOpt, usdRate, fxRate =
        match entry with
        | Open o -> (o.Id, o.Date, o.Start, None, o.UsdRate, o.FxCadPerUsd)
        | Closed c -> (c.Id, c.Date, c.Start, Some c.End, c.UsdRate, c.FxCadPerUsd)

    let (EntryId guid) = entryId
    let endTimeValue = endTimeOpt |> Option.map formatTime |> Option.defaultValue ""

    tr
        []
        [ td
              [ _colspan "8" ]
              [ form
                    [ _hx "post" "/entries/update"
                      _hx "target" "#entries-list"
                      _hx "swap" "outerHTML" ]
                    [ input [ _type "hidden"; _name "id"; _value (guid.ToString()) ]

                      div
                          [ _style "display: flex; gap: 10px; align-items: end;" ]
                          [ div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "date" ] [ str "Date" ]
                                  input
                                      [ _type "date"
                                        _name "date"
                                        _value (formatDate date)
                                        _max (now.ToString("yyyy-MM-dd"))
                                        _required ] ]

                            div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "start" ] [ str "Start" ]
                                  input [ _type "time"; _name "start"; _value (formatTime start); _required ] ]

                            div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "endTime" ] [ str "End" ]
                                  input [ _type "time"; _name "endTime"; _value endTimeValue ] ]

                            div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "usdRate" ] [ str "Rate (USD)" ]
                                  input
                                      [ _type "number"
                                        _name "usdRate"
                                        _step "0.01"
                                        _min "0"
                                        _value (string (stripRate usdRate))
                                        _required ] ]

                            div
                                [ _class "form-group"; _style "margin: 0;" ]
                                [ label [ _for "fxRate" ] [ str "FX Rate" ]
                                  input
                                      [ _type "number"
                                        _name "fxRate"
                                        _step "0.0001"
                                        _min "0"
                                        _value (string (stripFx fxRate))
                                        _required ] ]

                            button [ _type "submit"; _style "margin-right: 5px;" ] [ str "Save" ]
                            button
                                [ _type "button"
                                  _hx "get" $"/entries/%s{guid.ToString()}/cancel"
                                  _hx "target" "closest tr"
                                  _hx "swap" "outerHTML" ]
                                [ str "Cancel" ] ] ] ] ]

let entryDeleteConfirmRow (entry: Entry) =
    let entryId, date, start, endTimeOpt =
        match entry with
        | Open o -> (o.Id, o.Date, o.Start, None)
        | Closed c -> (c.Id, c.Date, c.Start, Some c.End)

    let (EntryId guid) = entryId

    let entryDesc =
        match endTimeOpt with
        | Some endTime -> $"%s{formatDate date} from %s{formatTime start} to %s{formatTime endTime}"
        | None -> $"%s{formatDate date} starting at %s{formatTime start} (open)"

    tr
        [ _class "error" ]
        [ td
              [ _colspan "8"; _style "padding: 20px;" ]
              [ p [ _style "margin: 0 0 10px 0; font-weight: bold;" ] [ str $"Delete this entry: %s{entryDesc}?" ]
                button
                    [ _hx "post" $"/entries/%s{guid.ToString()}/delete"
                      _hx "target" "#entries-list"
                      _hx "swap" "outerHTML"
                      _style "margin-right: 10px; background: #dc3545;" ]
                    [ str "Yes, Delete" ]
                button
                    [ _hx "get" $"/entries/%s{guid.ToString()}/cancel"
                      _hx "target" "closest tr"
                      _hx "swap" "outerHTML" ]
                    [ str "Cancel" ] ] ]


let entriesTable (entries: Entry list) =
    table
        []
        [ thead
              []
              [ tr
                    []
                    [ th [] [ str "Date" ]
                      th [] [ str "Start" ]
                      th [] [ str "End" ]
                      th [] [ str "Rate (USD/h)" ]
                      th [] [ str "FX Rate (CAD/USD)" ]
                      th [] [ str "Hours" ]
                      th [] [ str "Total (CAD)" ]
                      th [] [ str "Actions" ] ] ]
          tbody [ _id "entries-table-body" ] (entries |> List.map entryRow) ]

let private paginationControls (totalCount: int) (displayedCount: int) (showingAll: bool) =
    if showingAll || displayedCount >= totalCount then
        // No controls needed - showing everything
        div [] []
    else
        let remaining = totalCount - displayedCount
        let nextCount = min 25 remaining

        div
            [ _style "margin-top: 20px; padding: 15px; background: #f8f9fa; border-radius: 8px; text-align: center;" ]
            [ p
                  [ _style "margin: 0 0 10px 0; color: #666;" ]
                  [ str $"Showing {displayedCount} of {totalCount} entries" ]

              div
                  [ _style "display: flex; gap: 10px; justify-content: center;" ]
                  [ button
                        [ _hx "get" $"/entries/more?count={displayedCount + 25}"
                          _hx "target" "#entries-list-content"
                          _hx "swap" "innerHTML"
                          _style
                              "padding: 8px 16px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: 500;" ]
                        [ str $"Load More ({nextCount})" ]

                    button
                        [ _hx "get" "/entries/all"
                          _hx "target" "#entries-list-content"
                          _hx "swap" "innerHTML"
                          _style
                              "padding: 8px 16px; background: #6c757d; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: 500;" ]
                        [ str $"Show All ({remaining} more)" ] ] ]

// Inner content that gets swapped
let private entriesListContent (entries: Entry list) (displayCount: int) =
    let totalCount = List.length entries
    let displayedEntries = entries |> List.truncate displayCount
    let actualDisplayed = List.length displayedEntries
    let showingAll = actualDisplayed >= totalCount

    if List.isEmpty entries then
        p [] [ str "No entries yet. Start tracking your time!" ]
    else
        div
            []
            [ entriesTable displayedEntries
              paginationControls totalCount actualDisplayed showingAll ]

// Inner content for showing all entries
let private entriesListContentAll (entries: Entry list) =
    let totalCount = List.length entries

    if List.isEmpty entries then
        p [] [ str "No entries yet. Start tracking your time!" ]
    else
        div
            []
            [ entriesTable entries
              div
                  [ _style
                        "margin-top: 20px; padding: 10px; background: #d4edda; border-radius: 8px; text-align: center;" ]
                  [ p [ _style "margin: 0; color: #155724;" ] [ str $"Showing all {totalCount} entries" ] ] ]

// Helper to create the entries list with a specific display count
let entriesListViewWithCount (entries: Entry list) (displayCount: int) =
    div [ _id "entries-list-content" ] [ entriesListContent entries displayCount ]

let entriesListView (entries: Entry list) =
    div
        [ _id "entries-list"
          // Only refocus when the request came from the Add Entry form
          attr
              "hx-on::after-settle"
              "
            (function(){
              var e = window.$event || window.event;
              var d = e && e.detail || {};
              var initiator = d.elt;
              var isAddForm = initiator && initiator.id === 'add-entry-form';
              if (!isAddForm) return;

              // two RAFs to run after htmx focus restoration
              requestAnimationFrame(function(){
                requestAnimationFrame(function(){
                  var date = document.getElementById('date-input');
                  if (date) {
                    try { document.activeElement && document.activeElement.blur(); } catch(_) {}
                    date.focus();
                    try { date.select && date.select(); } catch(_) {}
                  }
                });
              });
            })();
          " ]
        [ div [ _id "entries-list-content" ] [ entriesListContent entries 25 ] ]

// Full entries list view (for "Show All" option)
let entriesListViewAll (entries: Entry list) =
    div [ _id "entries-list-content" ] [ entriesListContentAll entries ]

let addEntryForm (errors: string list option) =
    let now = DateTime.Now

    form
        [ _id "add-entry-form"
          _hx "post" "/entries"
          _hx "target" "#entries-list"
          _hx "swap" "outerHTML" ]
        [ h2 [] [ str "Add Entry" ]

          // Display errors if any
          match errors with
          | Some errs -> div [ _class "error" ] (errs |> List.map (fun e -> p [] [ str e ]))
          | None -> ()

          div
              [ _class "form-group" ]
              [ label [ _for "date" ] [ str "Date" ]
                input
                    [ _type "date"
                      _name "date"
                      _id "date-input"
                      attr "autofocus" "autofocus"
                      _value (now.ToString("yyyy-MM-dd"))
                      _max (now.ToString("yyyy-MM-dd"))
                      _required
                      attr "onchange" "fetchFxRate()" ] ]

          div
              [ _class "form-group" ]
              [ label [ _for "start" ] [ str "Start Time" ]
                input
                    [ _type "time"
                      _name "start"
                      _id "start"
                      _value (now.ToString("HH:mm"))
                      _required ] ]

          div
              [ _class "form-group" ]
              [ label [ _for "endTime" ] [ str "End Time (optional)" ]
                input [ _type "time"; _name "endTime"; _id "endTime" ]
                small
                    [ _style "display: block; color: #666; margin-top: 4px;" ]
                    [ str "(Leave blank to start tracking now)" ] ]

          div
              [ _class "form-group" ]
              [ label [ _for "usdRate" ] [ str "Hourly Rate (USD)" ]
                input
                    [ _type "number"
                      _name "usdRate"
                      _id "usdRate"
                      _step "0.01"
                      _min "0"
                      _required ] ]

          div
              [ _class "form-group" ]
              [ label [ _for "fxRate" ] [ str "Exchange Rate (CAD/USD)" ]
                input
                    [ _type "number"
                      _name "fxRate"
                      _id "fxRate"
                      _step "0.01"
                      _min "0"
                      _value "1.35"
                      _required ]
                small
                    [ _style "display: block; color: #666; margin-top: 4px;" ]
                    [ str "(Auto-fetched when you select a date, but you can override it)" ] ]

          button [ _type "submit" ] [ str "Add Entry" ]

          script
              []
              [ rawText
                    """
function fetchFxRate() {
    const date = document.getElementById('date-input').value;
    if (date) {
        fetch('/fx/' + date)
            .then(r => r.text())
            .then(rate => {
                document.getElementById('fxRate').value = rate;
            });
    }
}
// Fetch on page load for today's date
fetchFxRate();

// After any htmx settle of #entries-list, put focus back on the date field
document.body.addEventListener('htmx:afterSettle', function (evt) {
    const target = evt.detail && evt.detail.target;
    if (target && target.id === 'entries-list') {
        // run on next tick so it wins over htmx's own focus restore
        setTimeout(() => {
            const date = document.getElementById('date-input');
            if (date) {
                date.focus();
                try { date.select && date.select(); } catch (_) {}
            }
        }, 0);
    }
});

""" ] ]

let private exportAllEntriesSection () =
    div
        [ _style "margin: 20px 0; padding: 15px; background: #f8f9fa; border-radius: 8px; border: 1px solid #dee2e6;" ]
        [ h3 [ _style "margin: 0 0 10px 0; font-size: 16px; color: #495057;" ] [ str "📦 Backup & Export" ]

          p
              [ _style "margin: 0 0 15px 0; font-size: 14px; color: #666;" ]
              [ str "Export all entries (including open entries) as CSV for backup or external analysis." ]

          a
              [ _href "/entries/export/csv"
                _style
                    "padding: 8px 16px; background: #17a2b8; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: 500; text-decoration: none; display: inline-block;"
                _ariaLabel "Download all entries as CSV" ]
              [ str "📊 Download All Entries (CSV)" ] ]

let indexView (entries: Entry list) =
    let now = DateTime.Now

    Layout.layout
        "Time Entries"
        [ // Navigation bar
          div
              [ _style
                    "display: flex; gap: 10px; margin-bottom: 20px; padding-bottom: 20px; border-bottom: 2px solid #dee2e6;" ]
              [ a
                    [ _href "/"
                      _style
                          "padding: 10px 20px; background: #007bff; color: white; text-decoration: none; border-radius: 4px; font-weight: 600;" ]
                    [ str "Time Entries" ]

                a
                    [ _href $"/reports/monthly/{now.Year}/{now.Month}"
                      _style
                          "padding: 10px 20px; background: #6c757d; color: white; text-decoration: none; border-radius: 4px;" ]
                    [ str "Monthly Report" ]

                a
                    [ _href $"/reports/yearly/{now.Year}"
                      _style
                          "padding: 10px 20px; background: #6c757d; color: white; text-decoration: none; border-radius: 4px;" ]
                    [ str "Yearly Report" ] ]

          h1 [] [ str "Side Hustle Time Tracker" ]

          // Export section
          exportAllEntriesSection ()

          addEntryForm None

          h2 [] [ str "All Entries" ]
          entriesListView entries ]
