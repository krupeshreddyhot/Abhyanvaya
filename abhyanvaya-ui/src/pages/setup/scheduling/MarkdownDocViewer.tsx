import { useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, CircularProgress, Stack, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";

type Props = {
  /** Public path under /docs/... */
  docPath: string;
  title: string;
  backTo?: string;
  backLabel?: string;
};

/** Minimal markdown renderer — content is loaded from markdown files (not hardcoded). */
function renderMarkdown(md: string): string {
  const lines = md.replace(/\r\n/g, "\n").split("\n");
  const html: string[] = [];
  let inList = false;
  const flushList = () => {
    if (inList) {
      html.push("</ul>");
      inList = false;
    }
  };

  for (const raw of lines) {
    const line = raw.trimEnd();
    if (!line.trim()) {
      flushList();
      continue;
    }
    if (line.startsWith("### ")) {
      flushList();
      html.push(`<h3>${inline(line.slice(4))}</h3>`);
      continue;
    }
    if (line.startsWith("## ")) {
      flushList();
      html.push(`<h2>${inline(line.slice(3))}</h2>`);
      continue;
    }
    if (line.startsWith("# ")) {
      flushList();
      html.push(`<h1>${inline(line.slice(2))}</h1>`);
      continue;
    }
    if (line.startsWith("- ") || line.startsWith("* ")) {
      if (!inList) {
        html.push("<ul>");
        inList = true;
      }
      html.push(`<li>${inline(line.slice(2))}</li>`);
      continue;
    }
    if (/^\d+\.\s/.test(line)) {
      flushList();
      html.push(`<p>${inline(line)}</p>`);
      continue;
    }
    flushList();
    html.push(`<p>${inline(line)}</p>`);
  }
  flushList();
  return html.join("\n");
}

function inline(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>')
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/`([^`]+)`/g, "<code>$1</code>");
}

const MarkdownDocViewer = ({
  docPath,
  title,
  backTo = "/setup/scheduling",
  backLabel = "Scheduling",
}: Props) => {
  const [md, setMd] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(docPath);
        if (!res.ok) throw new Error(`Unable to load ${docPath}`);
        setMd(await res.text());
      } catch (e) {
        setError(e instanceof Error ? e.message : "Failed to load guide.");
      } finally {
        setLoading(false);
      }
    })();
  }, [docPath]);

  const html = useMemo(() => (md ? renderMarkdown(md) : ""), [md]);

  const exportPdf = () => window.print();

  if (loading) return <CircularProgress sx={{ m: 2 }} />;

  return (
    <Stack spacing={2} className="scheduling-md-doc" sx={{ p: { xs: 1.5, md: 2 } }}>
      <Box
        sx={{ display: "flex", gap: 1, alignItems: "center", flexWrap: "wrap" }}
        className="no-print"
      >
        <Button component={RouterLink} to={backTo} startIcon={<ArrowBackIcon />}>
          {backLabel}
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          {title}
        </Typography>
        <Button variant="outlined" startIcon={<PictureAsPdfIcon />} onClick={exportPdf}>
          Export PDF
        </Button>
      </Box>
      {error && <Alert severity="error">{error}</Alert>}
      <Box
        sx={{
          "& h1": { fontSize: "1.5rem", mt: 0 },
          "& h2": { fontSize: "1.2rem", mt: 3, borderBottom: 1, borderColor: "divider", pb: 0.5 },
          "& h3": { fontSize: "1.05rem", mt: 2 },
          "& p, & li": { color: "text.secondary" },
          "& a": { color: "primary.main" },
          "& ul": { pl: 2.5 },
          maxWidth: 720,
        }}
        dangerouslySetInnerHTML={{ __html: html }}
      />
      <style>{`@media print { .no-print { display: none !important; } body { background: white; } }`}</style>
    </Stack>
  );
};

export default MarkdownDocViewer;
