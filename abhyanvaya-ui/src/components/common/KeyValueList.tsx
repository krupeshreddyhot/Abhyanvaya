import { Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";

export type KeyValueItem = {
  label: string;
  value: ReactNode;
  /** Render the value in a monospace font with aggressive word-breaking — for URLs/keys/paths. */
  mono?: boolean;
};

export type KeyValueListProps = {
  items: KeyValueItem[];
};

/**
 * Generic read-only label/value list (AI20.UI.9's Enrollment Configuration panel). Purely
 * presentational and content-agnostic — any future read-only settings/config card can reuse this
 * instead of hand-rolling its own row layout.
 */
const KeyValueList = ({ items }: KeyValueListProps) => (
  <Stack spacing={1.25}>
    {items.map((item) => (
      <Stack
        key={item.label}
        direction={{ xs: "column", sm: "row" }}
        spacing={{ xs: 0.25, sm: 2 }}
        sx={{ justifyContent: "space-between", alignItems: { xs: "flex-start", sm: "center" } }}
      >
        <Typography variant="body2" color="text.secondary" sx={{ flexShrink: 0 }}>
          {item.label}
        </Typography>
        <Typography
          variant="body2"
          sx={{
            fontWeight: 600,
            textAlign: { sm: "right" },
            wordBreak: "break-word",
            ...(item.mono && { fontFamily: "monospace", fontSize: "0.8125rem" }),
          }}
        >
          {item.value}
        </Typography>
      </Stack>
    ))}
  </Stack>
);

export default KeyValueList;
