SELECT column_name
FROM information_schema.columns
WHERE table_name = 'AttendanceSession'
  AND column_name ILIKE '%image%'
ORDER BY column_name;

SELECT column_name
FROM information_schema.columns
WHERE table_name = 'AttendanceSessionImage'
ORDER BY ordinal_position;

SELECT i."Id", i."AttendanceSessionId", i."ImageSequence", i."ImageKey", i."Status", i."Width", i."Height"
FROM "AttendanceSessionImage" i
ORDER BY i."UploadedUtc" DESC NULLS LAST
LIMIT 8;

SELECT r."Id", r."AttendanceSessionId", r."FaceNumber", r."FaceImageKey", r."RecognitionStatus", r."StudentId"
FROM "AttendanceRecognition" r
ORDER BY r."CreatedUtc" DESC
LIMIT 10;
