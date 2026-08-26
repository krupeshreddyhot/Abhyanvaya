import { IconButton, Tooltip, Typography } from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";

export type AcademicHelpHintProps = {
  title: string;
  body: string;
};

/**
 * AI29.1D Prompt 17 — compact contextual help (Tooltip; reuses MUI, no new drawer system).
 */
export default function AcademicHelpHint({ title, body }: AcademicHelpHintProps) {
  return (
    <Tooltip
      arrow
      enterTouchDelay={400}
      title={
        <>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
            {title}
          </Typography>
          <Typography variant="caption" component="div">
            {body}
          </Typography>
        </>
      }
    >
      <IconButton size="small" aria-label={`Help: ${title}`} sx={{ p: 0.35 }}>
        <InfoOutlinedIcon fontSize="small" />
      </IconButton>
    </Tooltip>
  );
}
