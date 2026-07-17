import { Link as RouterLink } from "react-router-dom";
import { Box, Card, CardActionArea, CardContent, Stack, Typography } from "@mui/material";
import FaceRetouchingNaturalIcon from "@mui/icons-material/FaceRetouchingNatural";

type AiModuleLink = {
  to: string;
  title: string;
  description: string;
  icon: React.ReactNode;
};

/**
 * AI Center hub — Super Admin only landing page for AI-driven platform modules.
 * Navigation only; each module's own page owns its business logic.
 */
const modules: AiModuleLink[] = [
  {
    to: "/ai/enrollment",
    title: "Student Enrollment",
    description: "Bulk AI enrollment of student photographs — download, validate, and generate face embeddings.",
    icon: <FaceRetouchingNaturalIcon fontSize="large" color="primary" />,
  },
];

const AiCenterPage = () => {
  return (
    <Stack spacing={3}>
      <Typography variant="h4">AI Center</Typography>
      <Typography variant="body1" color="text.secondary">
        Super Admin tools for AI-driven platform capabilities.
      </Typography>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" },
          gap: 2,
        }}
      >
        {modules.map((m) => (
          <Card key={m.to} variant="outlined">
            <CardActionArea component={RouterLink} to={m.to} sx={{ height: "100%" }}>
              <CardContent>
                <Stack spacing={1.5} sx={{ alignItems: "flex-start" }}>
                  {m.icon}
                  <Typography variant="h6">{m.title}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {m.description}
                  </Typography>
                </Stack>
              </CardContent>
            </CardActionArea>
          </Card>
        ))}
      </Box>
    </Stack>
  );
};

export default AiCenterPage;
