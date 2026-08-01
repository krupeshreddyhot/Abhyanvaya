import { Box } from "@mui/material";

/** AI22.7B Phase 5.3 — skip link for keyboard users. */
export function SkipToContentLink({ targetId = "main-content" }: { targetId?: string }) {
  return (
    <Box
      component="a"
      href={`#${targetId}`}
      className="skip-link"
      data-skip-link
      sx={{
        position: "absolute",
        left: 8,
        top: 8,
        zIndex: (theme) => theme.zIndex.tooltip + 1,
        px: 2,
        py: 1,
        bgcolor: "primary.main",
        color: "primary.contrastText",
        borderRadius: 1,
        transform: "translateY(-200%)",
        transition: (theme) => theme.transitions.create("transform"),
        "&:focus": {
          transform: "translateY(0)",
        },
      }}
    >
      Skip to main content
    </Box>
  );
}

export default SkipToContentLink;
