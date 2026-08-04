## Summary

Describe the user-visible change and why it belongs in PdfiumRaster's PDF-to-image scope.

## Verification

List the commands, operating systems, and test PDFs used to verify the change. Do not attach confidential PDFs.

## Checklist

- [ ] I kept native PDFium calls in `PdfiumNative` and serialized through the shared lock.
- [ ] I added or updated focused tests for public behavior and native lifetime changes.
- [ ] I added XML documentation for every new public API and documented indexes, units, ownership, and memory behavior where relevant.
- [ ] I updated `README.md` and `docs/API.md` for user-facing API or behavior changes.
- [ ] I used central package management and did not add package versions to project files.
- [ ] I ran `make test` and, for packaging or release changes, `make pack`.
