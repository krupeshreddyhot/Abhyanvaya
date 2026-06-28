import PersonIcon from "@mui/icons-material/Person";
import VerifiedIcon from "@mui/icons-material/Verified";
import { Avatar, Box, Card, CardContent, Chip, Stack, Typography } from "@mui/material";

export type StudentProfileHeaderProps = {
  isEdit?: boolean;
  name?: string;
  studentNumber?: string;
  courseName?: string;
  semesterName?: string;
  batch?: string | number | null;
  photoUrl?: string | null;
  photoVerified?: boolean;
  hasPhoto?: boolean;
};

const PLACEHOLDER = {
  name: "Student Name",
  studentNumber: "Not assigned",
  course: "Select course",
  semester: "Select semester",
  batch: "Batch —",
} as const;

const resolveBatchLabel = (batch?: string | number | null, isEdit?: boolean): string => {
  if (batch === null || batch === undefined || batch === "") {
    return isEdit ? PLACEHOLDER.batch : PLACEHOLDER.batch;
  }
  return `Batch ${batch}`;
};

const resolveStatus = (
  isEdit: boolean,
  hasPhoto: boolean,
  photoVerified: boolean
): { label: string; color: "default" | "success" | "warning" | "info" } => {
  if (!isEdit) {
    return { label: "New Student", color: "default" };
  }
  if (!hasPhoto) {
    return { label: "No Photo", color: "default" };
  }
  if (photoVerified) {
    return { label: "Verified", color: "success" };
  }
  return { label: "Pending Verification", color: "warning" };
};

const StudentProfileHeader = ({
  isEdit = false,
  name = "",
  studentNumber = "",
  courseName = "",
  semesterName = "",
  batch = null,
  photoUrl = null,
  photoVerified = false,
  hasPhoto = false,
}: StudentProfileHeaderProps) => {
  const displayName = (isEdit ? name : name.trim()) || PLACEHOLDER.name;
  const displayStudentNumber =
    (isEdit ? studentNumber : studentNumber.trim()) || PLACEHOLDER.studentNumber;
  const displayCourse = courseName.trim() || PLACEHOLDER.course;
  const displaySemester = semesterName.trim() || PLACEHOLDER.semester;
  const displayBatch = resolveBatchLabel(batch, isEdit);
  const status = resolveStatus(isEdit, hasPhoto, photoVerified);
  const metaLine = [displayCourse, displaySemester, displayBatch].join(" · ");

  return (
    <Card
      variant="outlined"
      component="section"
      aria-labelledby="photo-card-title"
      sx={{ mb: 1.5, bgcolor: "background.default" }}
    >
      <CardContent sx={{ py: 1.5, px: { xs: 1.5, sm: 2 }, "&:last-child": { pb: 1.5 } }}>
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={2}
          sx={{ alignItems: { xs: "center", md: "flex-start" } }}
        >
          <Avatar
            src={photoUrl ?? undefined}
            alt={photoUrl ? `${displayName} profile photo` : "No profile photo"}
            sx={{
              width: 72,
              height: 72,
              border: 1,
              borderColor: "divider",
              bgcolor: "action.hover",
              color: "text.secondary",
            }}
          >
            {!photoUrl && <PersonIcon sx={{ fontSize: 40 }} aria-hidden />}
          </Avatar>

          <Box sx={{ flex: 1, minWidth: 0, textAlign: { xs: "center", md: "left" } }}>
            <Typography
              variant="h6"
              component="h2"
              id="student-profile-header-name"
              sx={{
                fontWeight: 700,
                letterSpacing: 0.4,
                textTransform: "uppercase",
                lineHeight: 1.3,
              }}
            >
              {displayName}
            </Typography>

            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
              Student No{" "}
              <Box component="span" sx={{ color: "text.primary", fontWeight: 600 }}>
                {displayStudentNumber}
              </Box>
            </Typography>

            <Typography
              variant="body2"
              color="text.primary"
              sx={{ mt: 1, lineHeight: 1.5 }}
            >
              {metaLine}
            </Typography>

            <Box sx={{ mt: 1 }}>
              <Chip
                icon={status.label === "Verified" ? <VerifiedIcon aria-hidden /> : undefined}
                label={status.label}
                size="small"
                color={status.color}
                variant={status.color === "default" ? "outlined" : "filled"}
                role="status"
                aria-label={`Student status: ${status.label}`}
              />
            </Box>
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
};

export default StudentProfileHeader;
