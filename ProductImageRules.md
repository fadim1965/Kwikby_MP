# Product photo rules (shop admin upload)

| Rule | Shop admin sees | Server does |
|---|---|---|
| Format | Must be PNG. Other formats are rejected. | Checks the file's signature bytes, not the extension. |
| File size | 5 MB or smaller. | Rejects larger files. |
| Minimum size | Shortest side at least 800 px. | Rejects smaller photos. The server never enlarges a photo. |
| Maximum size | 6000 px per side, 24 megapixels in total. | Rejects larger photos to protect the server. |
| Aspect ratio | Any ratio is accepted. | Centres the photo on a square white canvas, without stretching it. |
| Transparency | Accepted. | Flattens it onto white. |
| Metadata | Accepted. | Removes camera and GPS data. |
| Output size | Shown as 1200 x 1200 px. | Shrinks the longest side to 1200 px if larger. |
| File name | Any. | Replaced with a random GUID. |

Client-side checks (file type, size and dimensions) give fast feedback. The server checks are the ones that count.
