import { Link as RouterLink } from "react-router-dom";
import { Box, Button, Card, CardActionArea, CardContent, Link, Typography, Stack } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { PermissionKeys } from "../../auth/permissionKeys";
import { useAuth } from "../../context/AuthContext";

type HubLink = {
  to: string;
  title: string;
  description: string;
  anyPermission?: string[];
};

const links: HubLink[] = [
  {
    to: "/setup/departments",
    title: "Departments",
    description: "Academic departments within a college",
    anyPermission: [PermissionKeys.SetupDepartmentsManage],
  },
  {
    to: "/setup/staff",
    title: "Staff",
    description: "Faculty and staff directory, roles, and subject assignments",
    anyPermission: [PermissionKeys.SetupStaffManage],
  },
  {
    to: "/setup/courses",
    title: "Courses",
    description: "Programmes or streams",
    anyPermission: [PermissionKeys.SetupCoursesManage],
  },
  {
    to: "/setup/groups",
    title: "Groups",
    description: "Sections or batches within a course",
    anyPermission: [PermissionKeys.SetupGroupsManage],
  },
  {
    to: "/setup/semesters",
    title: "Semesters",
    description: "Terms linked to course and group",
    anyPermission: [PermissionKeys.SetupSemestersManage],
  },
  {
    to: "/setup/subjects",
    title: "Subjects",
    description: "Papers including language slots and electives",
    anyPermission: [PermissionKeys.SetupSubjectsManage],
  },
  {
    to: "/setup/languages",
    title: "Languages",
    description: "First & second language catalog",
    anyPermission: [PermissionKeys.SetupLookupsManage],
  },
  {
    to: "/setup/genders",
    title: "Genders",
    description: "Student gender options",
    anyPermission: [PermissionKeys.SetupLookupsManage],
  },
  {
    to: "/setup/mediums",
    title: "Mediums",
    description: "Instruction medium",
    anyPermission: [PermissionKeys.SetupLookupsManage],
  },
  {
    to: "/setup/elective-groups",
    title: "Elective groups",
    description: "Clusters for elective subject choice",
    anyPermission: [PermissionKeys.SetupLookupsManage],
  },
  {
    to: "/setup/staff-lookups",
    title: "Staff & department lookups",
    description: "Staff types, titles, roles, qualifications — values used on Staff and department assignments",
    anyPermission: [PermissionKeys.SetupLookupsManage],
  },
];

const SetupHub = () => {
  const { hasAnyPermission, hasPermission, user } = useAuth();
  const canCollegeProfile = hasPermission(PermissionKeys.OrganizationManage);
  const isTenantAdmin =
    (user?.role ?? "").toLowerCase() === "admin" && (user?.tenantId ?? 0) > 0;

  const visibleLinks = links.filter((x) =>
    x.anyPermission?.length ? hasAnyPermission(x.anyPermission) : false,
  );

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
        {canCollegeProfile && (
          <>
            {" "}
            Edit your college profile or import students under <strong>College profile</strong>.
          </>
        )}
      </Typography>

      {canCollegeProfile && (
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

      {isTenantAdmin && (
        <Card variant="outlined">
          <CardActionArea component={RouterLink} to="/setup/roles">
            <CardContent>
              <Typography variant="h6">Roles &amp; permissions</Typography>
              <Typography variant="body2" color="text.secondary">
                Application roles, permission sets, and which users hold each role (college administrators only)
              </Typography>
              <Box sx={{ mt: 1, display: "flex", flexWrap: "wrap", alignItems: "center", columnGap: 1, rowGap: 0.5 }}>
                <Link
                  href="/docs/tenant-admin-guide.html"
                  target="_blank"
                  rel="noopener noreferrer"
                  variant="body2"
                  onClick={(e) => e.stopPropagation()}
                  sx={{ display: "inline-flex", alignItems: "center", gap: 0.5, fontWeight: 600 }}
                >
                  College admin guide
                  <OpenInNewIcon fontSize="small" aria-hidden />
                </Link>
                <Typography variant="body2" color="text.secondary" component="span">
                  ·
                </Typography>
                <Link
                  href="/docs/index.html"
                  target="_blank"
                  rel="noopener noreferrer"
                  variant="body2"
                  onClick={(e) => e.stopPropagation()}
                  sx={{ display: "inline-flex", alignItems: "center", gap: 0.5 }}
                >
                  Technical docs &amp; CSV
                  <OpenInNewIcon fontSize="small" aria-hidden />
                </Link>
              </Box>
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
        {visibleLinks.map((x) => (
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
