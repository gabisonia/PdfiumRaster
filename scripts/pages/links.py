"""Keep repository links usable when only docs/ is published."""
from pathlib import Path
from urllib.parse import urlsplit, urlunsplit

from mkdocs.structure.files import File
from markdown.extensions import Extension
from markdown.treeprocessors import Treeprocessor


class RepositoryLinks(Treeprocessor):
    def run(self, root):
        for link in root.iter("a"):
            url = urlsplit(link.get("href", ""))
            if url.scheme or url.netloc or not url.path.startswith("../"):
                continue
            target = (self.source.parent / url.path).resolve()
            if target.is_relative_to(self.docs):
                continue
            relative = target.relative_to(self.docs.parent).as_posix()
            path = f"{self.repo}/blob/master/{relative}"
            link.set("href", urlunsplit(("", "", path, url.query, url.fragment)))


class RepositoryExtension(Extension):
    def extendMarkdown(self, md):
        processor = RepositoryLinks(md)
        processor.source = self.source
        processor.docs = self.docs
        processor.repo = self.repo
        md.treeprocessors.register(processor, "repository_links", 2)


def on_page_markdown(markdown, *, page, config, files):
    # MkDocs constructs a Markdown parser per page. Replace the previous
    # extension so relative links use this page's source directory.
    config.markdown_extensions = [
        ext for ext in config.markdown_extensions if not isinstance(ext, RepositoryExtension)
    ]
    extension = RepositoryExtension()
    extension.source = Path(page.file.abs_src_path or Path(config.docs_dir) / page.file.src_uri)
    if page.file.src_uri == "examples.md":
        page.edit_url = config.repo_url.rstrip("/") + "/blob/master/samples/README.md"
    extension.docs = Path(config.docs_dir)
    extension.repo = config.repo_url.rstrip("/")
    config.markdown_extensions.append(extension)


def on_files(files, *, config):
    # Publish the maintained samples without creating a second copy.
    source = Path(config.docs_dir).parent / "samples" / "README.md"
    files.append(File.generated(config, "examples.md", content=source.read_text(encoding="utf-8")))
    return files
