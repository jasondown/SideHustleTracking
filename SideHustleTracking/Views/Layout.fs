module SideHustleTracking.Views.Layout

open Giraffe.ViewEngine

let layout (pageTitle: string) (content: XmlNode list) =
    html [] [
        head [] [
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1" ]
            title [] [ str $"Side Hustle Tracker - %s{pageTitle}" ]
            // Simple CSS for basic styling
            style [] [
                rawText """
                body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; 
                       max-width: 1200px; margin: 0 auto; padding: 20px; }
                table { width: 100%; border-collapse: collapse; margin: 20px 0; }
                th, td { text-align: left; padding: 12px; border-bottom: 1px solid #ddd; }
                th { background-color: #f5f5f5; font-weight: 600; }
                .form-group { margin-bottom: 15px; }
                label { display: block; margin-bottom: 5px; font-weight: 500; }
                input, select { padding: 8px; border: 1px solid #ddd; border-radius: 4px; width: 200px; }
                button { padding: 10px 20px; background: #007bff; color: white; border: none; 
                        border-radius: 4px; cursor: pointer; }
                button:hover { background: #0056b3; }
                .error { color: #dc3545; font-size: 14px; margin-top: 5px; }
                .success { color: #28a745; padding: 10px; background: #d4edda; 
                          border-radius: 4px; margin: 10px 0; }
                .open-entry { background-color: #fff3cd; }
                .closed-entry { background-color: #d4edda; }
                """
            ]
            // htmx for interactivity
            script [ _src "https://unpkg.com/htmx.org@1.9.10" ] []
        ]
        body [] content
    ]
