# Side Hustle Tracking

![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

This is a play project I use for time tracking my side hustle. It is specifically set up for my needs, and is not a generic time tracking tool. With that in mind, it may or may not be useful for others.

This project was also to explore building an F# web application using Giraffe and HTMX rendering. Additionally, functional programming concepts and patterns were used along with the [F#+ library](https://fsprojects.github.io/FSharpPlus/).

## Features

- Time tracking entries (add, edit, delete).
- Montly and yearly reporting screens, with navigation.
- Report exports to Markdown (with preview for copy/paste or download) or CSV.
- Bi-directional drill-down/drill-up from monthly and yearly reporting screens.
- Persistence is done via CSV, rather than a DB at this time.
- Export all entries to CSV for backup.
- Conversion rates from USD to CAD are automatically downloaded (with snapshots), with option to override.
- Docker setup is included to run without the need for the local repository. See the `README-DOCKER.md` file.

## Sample screenshots

<img width="1621" height="1514" alt="image" src="https://github.com/user-attachments/assets/799c672b-e178-41c5-aa3e-1cd6ecf73f56" />

<img width="1557" height="276" alt="image" src="https://github.com/user-attachments/assets/9562e3e7-469f-40c1-9e30-4334a717684c" />

<img width="1561" height="1424" alt="image" src="https://github.com/user-attachments/assets/a707b65f-44b9-4f36-bfba-4f54c63d4b71" />

<img width="1558" height="1897" alt="image" src="https://github.com/user-attachments/assets/25224f74-d3c9-4ffb-ab56-e16e4d7035f2" />

<img width="1536" height="884" alt="image" src="https://github.com/user-attachments/assets/7704f18c-5a01-44be-b385-4b7e69bd4a36" />

<img width="1334" height="1337" alt="image" src="https://github.com/user-attachments/assets/8ee06ba4-991f-400c-9bf8-c5ccb8f54571" />

## License

This project is licensed under the MIT License. See [LICENSE](./LICENSE) for details.
