import { Link as RouterLink } from "react-router-dom";
import { Box, Button, Card, CardActionArea, CardContent, Typography, Stack } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { useAuth } from "../../context/AuthContext";

const links: { to: string; title: string; description: string }[] = [
  { to: "/setup/courses", title: "Courses", description: "Programmes or streams" },
  { to: "/setup/groups", title: "Groups", description: "Sections or batches within a course" },
  { to: "/setup/semesters", title: "Semesters", description: "Terms linked to course and group" },
  { to: "/setup/subjects", title: "Subjects", description: "Papers including language slots and electives" },
  { to: "/setup/languages", title: "Languages", description: "First & second language catalog" },
  { to: "/setup/genders", title: "Genders", description: "Student gender options" },
  { to: "/setup/mediums", title: "Mediums", description: "Instruction medium" },
  { to: "/setup/elective-groups", title: "Elective groups", description: "Clusters for elective subject choice" },
];

const SetupHub = () => {
  const role = (useAuth().user?.role ?? "").toLowerCase();
  const isAdmin = role === "admin";

  return (
    <Stack spacing={3}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/dashboard" startIcon={<ArrowBackIcon />} variant="text">
          Dashboard
        </Button>
        <Typography variant="h4" sx={{ flexGrow: 1 }}>
          Catalog
        </Typography>
      </Box>

      <Typography variant="body1" color="text.secondary">
        Maintain reference data used across students, attendance, and reports.
        {isAdmin && (
          <>
            {" "}
            Edit your college profile or import students under <strong>College profile</strong>.
          </>
        )}
      </Typography>

      {isAdmin && (
        <Card variant="outlined">
          <CardActionArea component={RouterLink} to="/setup/college">
            <CardContent>
              <Typography variant="h6">College profile</Typography>
              <Typography variant="body2" color="text.secondary">
                College name, code, university linkage, branding, Excel student import (edit only — no new college
                tenant here)
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>
      )}

      <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
        Reference lists
      </Typography>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" },
          gap: 2,
        }}
      >
        {links.map((x) => (
          <Card key={x.to} variant="outlined">
            <CardActionArea component={RouterLink} to={x.to}>
              <CardContent>
                <Typography variant="h6">{x.title}</Typography>
                <Typography variant="body2" color="text.secondary">
                  {x.description}
                </Typography>
              </CardContent>
            </CardActionArea>
          </Card>
        ))}
      </Box>
    </Stack>
  );
};

export default SetupHub;
