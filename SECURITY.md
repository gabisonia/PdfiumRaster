# Security Policy

## Supported Versions

Security fixes are released in the latest available PdfiumRaster version. Older versions are not guaranteed to
receive backported fixes. Because PDF parsing is performed by native PDFium code, dependency updates may be security
relevant even when the managed API is unchanged.

## Reporting A Vulnerability

Do not disclose a suspected vulnerability or a proof-of-concept document in a public issue.

If it is available for the repository, use
[GitHub's private vulnerability reporting](https://github.com/gabisonia/PdfiumRaster/security/advisories/new). Otherwise,
open a public issue asking the maintainer for a private contact channel, without including exploit details or a
sensitive PDF. Include the affected version, operating system and architecture, impact, reproduction conditions, and
whether the report may be disclosed after a fix is available.

There is no guaranteed response or remediation SLA. Reports will be assessed based on reproducibility, impact, and
whether the issue is in PdfiumRaster, its native dependencies, or the consuming application.

## Using PdfiumRaster With Untrusted PDFs

PdfiumRaster does not sandbox PDFium. Applications should keep the package and its dependencies current, validate
inputs, cap page count and render dimensions, set time and memory limits outside the library, and use a separately
supervised worker process when crash or resource isolation is required.
