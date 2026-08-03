SELECT i."AttendanceSessionId", i."ImageKey", i."ImageHash", i."FileSize", i."Status"
FROM "AttendanceSessionImage" i
WHERE i."AttendanceSessionId" IN (
  'fb0d7fb1-8879-4a22-a53f-ec4781b8a20a',
  '09917b91-7bcd-43bf-a715-4f1373834899',
  '085061d8-9131-4fe5-bd55-67b883fb2f4f'
);

SELECT r."AttendanceSessionId", r."FaceNumber", r."FaceImageKey"
FROM "AttendanceRecognition" r
WHERE r."AttendanceSessionId" = 'fb0d7fb1-8879-4a22-a53f-ec4781b8a20a'
ORDER BY r."FaceNumber";
