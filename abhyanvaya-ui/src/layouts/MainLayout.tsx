import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  Drawer,
  List,
  ListItemText,
  ListItemButton,
  ListItemIcon,
  IconButton,
  Button,
  Collapse,
} from "@mui/material";
import AccessibilityNewIcon from "@mui/icons-material/AccessibilityNew";
import MenuIcon from "@mui/icons-material/Menu";
import DashboardIcon from "@mui/icons-material/Dashboard";
import PeopleIcon from "@mui/icons-material/People";
import EventNoteIcon from "@mui/icons-material/EventNote";
import BarChartIcon from "@mui/icons-material/BarChart";
import CategoryIcon from "@mui/icons-material/Category";
import BusinessIcon from "@mui/icons-material/Business";
// AI20.UI.1: Psychology is the preferred "AI Center" glyph; AutoAwesome is the documented fallback
// (see docs/AI20_UI1_VISUAL_IDENTITY_NOTE.md) if a future icon-package downgrade ever removes it.
import PsychologyIcon from "@mui/icons-material/Psychology";
import FaceRetouchingNaturalIcon from "@mui/icons-material/FaceRetouchingNatural";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { alpha, useTheme } from "@mui/material/styles";
import useMediaQuery from "@mui/material/useMediaQuery";
import { useMemo, useState } from "react";
import { Outlet, useNavigate, useLocation, Link as RouterLink } from "react-router-dom";
import { PermissionKeys } from "../auth/permissionKeys";
import { useAuth } from "../context/AuthContext";
import { useEffect } from "react";
import { getHeaderInfo, type HeaderInfo } from "../services/uiService";
import { brandingAssetUrl } from "../utils/brandingUrl";
import {
  ReviewFullscreenProvider,
  useReviewFullscreen,
} from "../context/ReviewFullscreenContext";
import {
  AccessibilityReportDialog,
  SkipToContentLink,
  ThemeModeToggle,
  WorkspaceProfileMenu,
} from "../theme";

const drawerWidth = 240;

// AI20.UI.1 / AI22.7B-R3 — accents from enterprise theme tokens (no hardcoded hex).
const OPERATIONAL_SELECTED_SX = {
  backgroundColor: (theme: { palette: { primary: { main: string } } }) =>
    alpha(theme.palette.primary.main, 0.12),
  color: "primary.main",
};
const AI_SELECTED_SX = {
  backgroundColor: (theme: { palette: { recognition: { aiAccent: string } } }) =>
    alpha(theme.palette.recognition.aiAccent, 0.12),
  color: (theme: { palette: { recognition: { aiAccent: string } } }) =>
    theme.palette.recognition.aiAccent,
};

type MenuVisibilityCtx = { role: string; hasPermission: (k: string) => boolean; hasAnyPermission: (k: string[]) => boolean };

type MenuItem = {
  text: string;
  icon: React.ReactNode;
  path: string;
  visible: (ctx: MenuVisibilityCtx) => boolean;
  /** Expandable submenu (e.g. AI Center → Student Enrollment). Parent row navigates to `path` and toggles this list. */
  children?: MenuItem[];
  /** Marks this item (and any children) as belonging to the AI Center visual family (AI20.UI.1). */
  accent?: "ai";
};

const MainLayoutChrome = () => {
  const { logout, user, hasPermission, hasAnyPermission } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const { fullscreen } = useReviewFullscreen();

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const userRole = (user?.role ?? "").toLowerCase();

  const catalogSetupPermissions = [
    PermissionKeys.SetupDepartmentsManage,
    PermissionKeys.SetupStaffManage,
    PermissionKeys.SetupSubjectsManage,
    PermissionKeys.SetupLookupsManage,
    PermissionKeys.SetupCoursesManage,
    PermissionKeys.SetupGroupsManage,
    PermissionKeys.SetupSemestersManage,
    PermissionKeys.OrganizationManage,
    PermissionKeys.SchedulingView,
    PermissionKeys.SchedulingManage,
    PermissionKeys.SchedulingRoomAvailabilityView,
    PermissionKeys.SchedulingRoomAvailabilityManage,
    PermissionKeys.SchedulingFacultyAvailabilityView,
    PermissionKeys.SchedulingFacultyAvailabilityManage,
    PermissionKeys.SchedulingTemplateView,
    PermissionKeys.SchedulingTemplateManage,
    PermissionKeys.SchedulingFacultyPreferencesView,
    PermissionKeys.SchedulingFacultyPreferencesManage,
    PermissionKeys.SchedulingRoomFeaturesView,
    PermissionKeys.SchedulingRoomFeaturesManage,
    PermissionKeys.SchedulingSubjectDeliveryView,
    PermissionKeys.SchedulingSubjectDeliveryManage,
    PermissionKeys.SchedulingHolidayTypesView,
    PermissionKeys.SchedulingHolidayTypesManage,
    PermissionKeys.SchedulingTimetableView,
    PermissionKeys.SchedulingTimetableManage,
    PermissionKeys.SchedulingTeachingGroupView,
    PermissionKeys.SchedulingTeachingGroupManage,
    PermissionKeys.SchedulingVersionView,
    PermissionKeys.SchedulingVersionManage,
    PermissionKeys.SchedulingReview,
    PermissionKeys.SchedulingApprove,
    PermissionKeys.SchedulingPublish,
    PermissionKeys.SchedulingArchive,
    PermissionKeys.SchedulingClone,
    PermissionKeys.SchedulingHistoryView,
  ];

  const menuItems: MenuItem[] = [
    {
      text: "Dashboard",
      icon: <DashboardIcon />,
      path: "/dashboard",
      visible: ({ hasPermission: hp }) => hp(PermissionKeys.DashboardView),
    },
    {
      text: "Notifications",
      icon: <EventNoteIcon />,
      path: "/dashboard/notifications",
      visible: ({ hasPermission: hp }) => hp(PermissionKeys.DashboardView),
    },
    {
      text: "Health Center",
      icon: <BarChartIcon />,
      path: "/dashboard/health",
      visible: ({ hasPermission: hp }) => hp(PermissionKeys.DashboardView),
    },
    {
      text: "Faculty Workspace",
      icon: <EventNoteIcon />,
      path: "/faculty",
      visible: ({ hasPermission: hp }) => hp(PermissionKeys.AttendanceManage),
    },
    {
      text: "Students",
      icon: <PeopleIcon />,
      path: "/students",
      visible: ({ hasPermission: hp }) => hp(PermissionKeys.StudentsView),
    },
    {
      text: "Attendance",
      icon: <EventNoteIcon />,
      path: "/attendance",
      visible: ({ hasPermission: hp }) => hp(PermissionKeys.AttendanceManage),
    },
    {
      text: "Reports",
      icon: <BarChartIcon />,
      path: "/reports",
      visible: ({ hasPermission: hp }) => hp(PermissionKeys.ReportsView),
    },
    {
      text: "Catalog",
      icon: <CategoryIcon />,
      path: "/setup",
      visible: ({ role, hasAnyPermission: anyPerm }) =>
        role === "admin" || anyPerm(catalogSetupPermissions),
    },
    {
      text: "Organization",
      icon: <BusinessIcon />,
      path: "/admin-setup",
      visible: ({ role }) => role === "superadmin",
    },
    {
      text: "AI Center",
      icon: <PsychologyIcon />,
      path: "/ai",
      visible: ({ role }) => role === "superadmin",
      accent: "ai",
      children: [
        {
          text: "Student Enrollment",
          icon: <FaceRetouchingNaturalIcon />,
          path: "/ai/enrollment",
          visible: ({ role }) => role === "superadmin",
          accent: "ai",
        },
      ],
    },
  ];

  const visibilityCtx = { role: userRole, hasPermission, hasAnyPermission };
  const visibleMenuItems = menuItems
    .filter((item) => item.visible(visibilityCtx))
    .map((item) => ({
      ...item,
      children: item.children?.filter((child) => child.visible(visibilityCtx)),
    }));

  const isItemActive = (item: MenuItem) =>
    location.pathname === item.path || location.pathname.startsWith(`${item.path}/`);

  const [mobileOpen, setMobileOpen] = useState(false);
  const [header, setHeader] = useState<HeaderInfo | null>(null);
  const [logoFailed, setLogoFailed] = useState(false);
  const [a11yOpen, setA11yOpen] = useState(false);

  useEffect(() => {
    const loadHeader = async () => {
      try {
        const res = await getHeaderInfo();
        setHeader(res.data);
      } catch {
        setHeader({
          fullName: "College",
          shortName: "College",
          role: user?.role ?? "",
        });
      }
    };

    void loadHeader();
    const refresh = () => void loadHeader();
    window.addEventListener("abhyanvaya:header-refresh", refresh);
    return () => window.removeEventListener("abhyanvaya:header-refresh", refresh);
  }, []);

  const handleDrawerToggle = () => {
    setMobileOpen(!mobileOpen);
  };

  const headerTitleFull = header?.fullName ?? "College";
  const headerTitleShort = header?.shortName ?? "College";

  const logoMd = brandingAssetUrl(header?.logoMdPath);
  const logoSrcSet = useMemo(() => {
    const sm = brandingAssetUrl(header?.logoSmPath);
    const md = brandingAssetUrl(header?.logoMdPath);
    const lg = brandingAssetUrl(header?.logoLgPath);
    if (!sm || !md || !lg) return undefined;
    return `${sm} 64w, ${md} 128w, ${lg} 256w`;
  }, [header?.logoSmPath, header?.logoMdPath, header?.logoLgPath]);

  useEffect(() => {
    setLogoFailed(false);
  }, [logoMd]);

  return (
    <Box sx={{ display: "flex", width: "100%", minWidth: 0, minHeight: "100vh", boxSizing: "border-box" }}>
      <SkipToContentLink />

      {!fullscreen && (
      <AppBar
        position="fixed"
        sx={{
          zIndex: 1201,
          ml: isMobile ? 0 : `${drawerWidth}px`,
          width: isMobile ? "100%" : `calc(100% - ${drawerWidth}px)`,
        }}
      >
        <Toolbar sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          {/* Left side: menu icon (mobile) + title */}
          <Box sx={{ display: "flex", alignItems: "center", flexGrow: 1, minWidth: 0 }}>
            <IconButton
              color="inherit"
              edge="start"
              onClick={handleDrawerToggle}
              aria-label="Open navigation menu"
              sx={{ mr: 1, display: { xs: "inline-flex", sm: "none" } }}
            >
              <MenuIcon />
            </IconButton>

            {logoMd && !logoFailed && (
              <Box
                component="img"
                src={logoMd}
                srcSet={logoSrcSet}
                sizes="(max-width: 600px) 36px, 44px"
                alt=""
                loading="lazy"
                onError={() => setLogoFailed(true)}
                sx={{
                  height: { xs: 34, sm: 40 },
                  width: "auto",
                  maxWidth: { xs: 120, sm: 180 },
                  objectFit: "contain",
                  mr: { xs: 1, sm: 1.5 },
                  flexShrink: 0,
                }}
              />
            )}

            <Typography
              variant="subtitle2"
              noWrap
              sx={{
                display: { xs: "none", sm: "block" },
                fontWeight: 600,
                maxWidth: { sm: 420, md: 560 },
              }}
            >
              {headerTitleFull}
            </Typography>

            <Typography
              variant="subtitle2"
              noWrap
              sx={{
                display: { xs: "block", sm: "none" },
                fontWeight: 600,
              }}
            >
              {headerTitleShort}
            </Typography>
          </Box>

          {/* Right side: theme / profile / a11y / role + logout */}
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
            <ThemeModeToggle />
            <WorkspaceProfileMenu />
            <IconButton
              color="inherit"
              size="small"
              onClick={() => setA11yOpen(true)}
              aria-label="Open accessibility report"
            >
              <AccessibilityNewIcon fontSize="small" />
            </IconButton>
            <Typography variant="body2" sx={{ display: { xs: "none", sm: "block" }, ml: 0.5 }}>
              {user?.role || "User"}
            </Typography>
            <Button color="inherit" size={isMobile ? "small" : "medium"} component={RouterLink} to="/change-password">
              Change password
            </Button>
            <Button
              color="inherit"
              size={isMobile ? "small" : "medium"}
              onClick={() => {
                logout();
                navigate("/");
              }}
            >
              Logout
            </Button>
          </Box>
        </Toolbar>
      </AppBar>
      )}

      {!fullscreen && (
      <Drawer
        variant={isMobile ? "temporary" : "permanent"}
        open={isMobile ? mobileOpen : true}
        onClose={handleDrawerToggle}
        ModalProps={
          isMobile
            ? {
                keepMounted: false,
              }
            : undefined
        }
        sx={{
          // Temporary drawer is an overlay — do not reserve 240px in the flex row (was shifting main content right on mobile).
          ...(isMobile
            ? { width: 0, flexShrink: 0 }
            : { width: drawerWidth, flexShrink: 0 }),
          [`& .MuiDrawer-paper`]: {
            width: drawerWidth,
            boxSizing: "border-box",
            top: 0,
          },
        }}
      >
        <Box
          sx={{
            px: 1,
            pb: 0.5,
            pt: 0.25,
            display: "flex",
            justifyContent: "center",
            borderBottom: "1px solid",
            borderColor: "divider",
            mb: 1,
          }}
        >
          <Box
            component="img"
            src="/abhyanvaya-logo.png"
            alt="Abhyanvaya logo"
            sx={{
              width: "100%",
              maxWidth: 160,
              height: "auto",
              objectFit: "contain",
            }}
          />
        </Box>
        <List>
          {visibleMenuItems.map((item) => {
            const hasChildren = !!item.children?.length;
            const parentSelected =
              item.path === "/setup" || hasChildren
                ? isItemActive(item)
                : location.pathname === item.path;
            const isAiAccent = item.accent === "ai";
            const selectedSx = isAiAccent ? AI_SELECTED_SX : OPERATIONAL_SELECTED_SX;

            return (
              <Box key={item.text}>
                <ListItemButton
                  selected={parentSelected}
                  onClick={() => {
                    navigate(item.path);
                    if (isMobile && !hasChildren) setMobileOpen(false);
                  }}
                  sx={{
                    // Resting-state icon tint keeps AI Center visually distinct even when not selected.
                    ...(isAiAccent && {
                      color: (theme) => theme.palette.recognition.aiAccent,
                    }),
                    "&.Mui-selected": selectedSx,
                    "&.Mui-selected:hover": selectedSx,
                  }}
                >
                  <ListItemIcon
                    sx={
                      isAiAccent
                        ? { color: (theme) => theme.palette.recognition.aiAccent }
                        : undefined
                    }
                  >
                    {item.icon}
                  </ListItemIcon>
                  <ListItemText primary={item.text} />
                  {hasChildren && (isItemActive(item) ? <ExpandLessIcon /> : <ExpandMoreIcon />)}
                </ListItemButton>

                {hasChildren && (
                  <Collapse in={isItemActive(item)} timeout="auto" unmountOnExit>
                    <List component="div" disablePadding>
                      {item.children!.map((child) => {
                        const childIsAiAccent = child.accent === "ai";
                        const childSelectedSx = childIsAiAccent ? AI_SELECTED_SX : OPERATIONAL_SELECTED_SX;

                        return (
                          <ListItemButton
                            key={child.text}
                            selected={location.pathname === child.path}
                            onClick={() => {
                              navigate(child.path);
                              if (isMobile) setMobileOpen(false);
                            }}
                            sx={{
                              pl: 4,
                              ...(childIsAiAccent && {
                                color: (theme) => theme.palette.recognition.aiAccent,
                              }),
                              "&.Mui-selected": childSelectedSx,
                              "&.Mui-selected:hover": childSelectedSx,
                            }}
                          >
                            <ListItemIcon
                              sx={
                                childIsAiAccent
                                  ? { color: (theme) => theme.palette.recognition.aiAccent }
                                  : undefined
                              }
                            >
                              {child.icon}
                            </ListItemIcon>
                            <ListItemText primary={child.text} />
                          </ListItemButton>
                        );
                      })}
                    </List>
                  </Collapse>
                )}
              </Box>
            );
          })}
        </List>
      </Drawer>
      )}

      <Box
        component="main"
        id="main-content"
        tabIndex={-1}
        sx={{
          flexGrow: 1,
          width: "100%",
          minWidth: 0,
          // AI22.7B 5.5 — ultrawide / 4K content comfort without redesign
          maxWidth: 1920,
          mx: "auto",
          p: fullscreen ? 1 : 2,
          pt: fullscreen ? 1 : 10,
          pb: fullscreen ? 1 : 6,
          boxSizing: "border-box",
          minHeight: fullscreen ? "100vh" : undefined,
          bgcolor: fullscreen ? "background.default" : undefined,
          outline: "none",
        }}
      >
        <Outlet />
      </Box>

      <AccessibilityReportDialog open={a11yOpen} onClose={() => setA11yOpen(false)} />

      {!fullscreen && (
      <Box
        sx={{
          position: "fixed",
          bottom: 0,
          left: 0,
          right: 0,
          textAlign: "center",
          p: 1,
          bgcolor: "background.paper",
          borderTop: 1,
          borderColor: "divider",
        }}
      >
        <Typography variant="body2" color="text.secondary">
          © Abhyanvaya 2026 - All Rights Reserved
        </Typography>
      </Box>
      )}
    </Box>
  );
};

const MainLayout = () => (
  <ReviewFullscreenProvider>
    <MainLayoutChrome />
  </ReviewFullscreenProvider>
);

export default MainLayout;
