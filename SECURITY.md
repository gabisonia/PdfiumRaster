# Security Policy

## Supported Versions

Security fixes are released in the latest available PdfiumRaster version. Older versions are not guaranteed to
receive backported fixes. Because PDF parsing is performed by native PDFium code, dependency updates may be security
relevant even when the managed API is unchanged.

## Reporting A Vulnerability

Do not disclose a suspected vulnerability or a proof-of-concept document in a public issue.

Use [GitHub's private vulnerability reporting](https://github.com/gabisonia/PdfiumRaster/security/advisories/new).
Do not open a public issue containing exploit details or a sensitive PDF. Include the affected version, operating
system and architecture, impact, reproduction conditions, and whether the report may be disclosed after a fix is
available.

## Response And Remediation Targets

- A maintainer will acknowledge a private vulnerability report within 14 calendar days.
- Confirmed vulnerabilities in PdfiumRaster will be assessed and coordinated through a private GitHub security
  advisory until a fix and disclosure are ready.
- A publicly known vulnerability of medium or higher severity will be fixed within 60 days. If a complete fix cannot
  be released in that period, the project will publish the current status, available mitigations, and expected next
  steps without disclosing information that would put users at additional risk.
- Critical vulnerabilities are prioritized for the earliest practical fix and release.

Reports are assessed based on reproducibility, impact, and whether the issue is in PdfiumRaster, its native
dependencies, or the consuming application. Dependency vulnerabilities are handled by updating or constraining the
affected dependency and publishing appropriate upgrade guidance.

## Automated Security Checks

The repository uses Dependabot alerts and update pull requests, dependency review, CodeQL code scanning, secret
scanning with push protection, and OpenSSF Scorecard analysis. These checks reduce risk but do not replace review of
native PDFium updates or application-level isolation for untrusted documents.

## Using PdfiumRaster With Untrusted PDFs

PdfiumRaster does not sandbox PDFium. Applications should keep the package and its dependencies current, validate
inputs, cap page count and render dimensions, set time and memory limits outside the library, and use a separately
supervised worker process when crash or resource isolation is required.
