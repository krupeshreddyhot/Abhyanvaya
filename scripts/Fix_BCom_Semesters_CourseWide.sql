-- Fix: B.Com semesters were pinned to Group FINANCE only, so COMPUTER APPLICATIONS
-- showed "No semesters for the selected Course and Group."
--
-- Course-wide Semesters (GroupId NULL) appear under every Group of the Course.
-- Safe when all existing Semester rows for that Course share the same GroupId
-- (i.e. no per-group semester variants already exist).

BEGIN;

UPDATE "Semester" s
SET "GroupId" = NULL,
    "UpdatedDate" = NOW()
WHERE s."IsDeleted" = FALSE
  AND s."GroupId" IS NOT NULL
  AND EXISTS (
    SELECT 1
    FROM "Group" g
    WHERE g."CourseId" = s."CourseId"
      AND g."IsDeleted" = FALSE
    GROUP BY g."CourseId"
    HAVING COUNT(*) > 1
  )
  AND NOT EXISTS (
    SELECT 1
    FROM "Semester" s2
    WHERE s2."CourseId" = s."CourseId"
      AND s2."IsDeleted" = FALSE
      AND s2."GroupId" IS DISTINCT FROM s."GroupId"
      AND s2."GroupId" IS NOT NULL
  );

-- Verify B.Com (Course Id 1 in local DB):
-- SELECT "Id", "Number", "Name", "GroupId" FROM "Semester" WHERE "CourseId" = 1 ORDER BY "Number";

COMMIT;
