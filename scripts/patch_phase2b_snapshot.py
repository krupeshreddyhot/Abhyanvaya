from pathlib import Path

p = Path(r"D:\Resheta\AttendenceProject\Abhyanvaya\Abhyanvaya.Infrastructure\Migrations\ApplicationDbContextModelSnapshot.cs")
c = p.read_text(encoding="utf-8")
if "Scheduling.Conflict.View" in c:
    print("already patched")
    raise SystemExit(0)

needle = (
    '                            Id = 53,\n'
    '                            Action = "Manage",\n'
    '                            Key = "Scheduling.Archive.Manage",\n'
    '                            Resource = "Scheduling.Archive"\n'
    "                        });"
)
insert = (
    '                            Id = 53,\n'
    '                            Action = "Manage",\n'
    '                            Key = "Scheduling.Archive.Manage",\n'
    '                            Resource = "Scheduling.Archive"\n'
    "                        },\n"
    "                        new\n"
    "                        {\n"
    "                            Id = 54,\n"
    '                            Action = "View",\n'
    '                            Key = "Scheduling.Conflict.View",\n'
    '                            Resource = "Scheduling.Conflict"\n'
    "                        },\n"
    "                        new\n"
    "                        {\n"
    "                            Id = 55,\n"
    '                            Action = "Manage",\n'
    '                            Key = "Scheduling.Conflict.Manage",\n'
    '                            Resource = "Scheduling.Conflict"\n'
    "                        });"
)
if needle not in c:
    # try CRLF
    needle = needle.replace("\n", "\r\n")
    insert = insert.replace("\n", "\r\n")
if needle not in c:
    raise SystemExit("perm needle missing")
c = c.replace(needle, insert, 1)

needle2 = (
    "                            ApplicationRoleId = 100,\n"
    "                            PermissionId = 53\n"
    "                        },\n"
    "                        new\n"
    "                        {\n"
    "                            ApplicationRoleId = 101,"
)
insert2 = (
    "                            ApplicationRoleId = 100,\n"
    "                            PermissionId = 53\n"
    "                        },\n"
    "                        new\n"
    "                        {\n"
    "                            ApplicationRoleId = 100,\n"
    "                            PermissionId = 54\n"
    "                        },\n"
    "                        new\n"
    "                        {\n"
    "                            ApplicationRoleId = 100,\n"
    "                            PermissionId = 55\n"
    "                        },\n"
    "                        new\n"
    "                        {\n"
    "                            ApplicationRoleId = 101,"
)
if needle2 not in c:
    needle2 = needle2.replace("\n", "\r\n")
    insert2 = insert2.replace("\n", "\r\n")
if needle2 not in c:
    raise SystemExit("role needle missing")
c = c.replace(needle2, insert2, 1)
p.write_text(c, encoding="utf-8")
print("snapshot patched ok")
