SELECT
  "Id",
  LEFT(COALESCE("AnnotatedImageKey", ''), 90) AS annotated,
  LEFT(COALESCE("OriginalImageKey", ''), 90) AS original
FROM "AttendanceSession"
ORDER BY "CreatedUtc" DESC
LIMIT 5;
