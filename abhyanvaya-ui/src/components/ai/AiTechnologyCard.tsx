import { Card, CardContent, Chip, Typography } from "@mui/material";
import KeyValueList, { type KeyValueItem } from "../common/KeyValueList";

/**
 * Read-only "Current AI Stack" summary (AI20.UI.16) — lets a support engineer identify the active
 * AI technologies at a glance. Mock/static values only; no backend, no API. Reuses the generic
 * `KeyValueList` primitive.
 *
 * Recognition Threshold intentionally renders the literal chip "Configured" rather than a hardcoded
 * numeric value: the real threshold may be loaded dynamically in a future version, and surfacing a
 * fake number here would be misleading.
 */
const AI_STACK_ITEMS: KeyValueItem[] = [
  { label: "Embedding Engine", value: "InsightFace" },
  { label: "Recognition Engine", value: "InsightFace" },
  { label: "Embedding Size", value: "512" },
  { label: "Similarity Metric", value: "Cosine Similarity" },
  { label: "Photo Provider", value: "ExamBranch" },
  { label: "Media Storage", value: "Cloudflare R2" },
  {
    label: "Recognition Threshold",
    value: <Chip size="small" label="Configured" variant="outlined" color="info" />,
  },
];

const AiTechnologyCard = () => (
  <Card variant="outlined">
    <CardContent>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
        Current AI Stack
      </Typography>
      <KeyValueList items={AI_STACK_ITEMS} />
    </CardContent>
  </Card>
);

export default AiTechnologyCard;
