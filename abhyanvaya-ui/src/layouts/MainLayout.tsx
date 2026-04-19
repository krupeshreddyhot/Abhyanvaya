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
  CssBaseline,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import DashboardIcon from "@mui/icons-material/Dashboard";
import PeopleIcon from "@mui/icons-material/People";
import EventNoteIcon from "@mui/icons-material/EventNote";
import BarChartIcon from "@mui/icons-material/BarChart";
import CategoryIcon from "@mui/icons-material/Category";
import BusinessIcon from "@mui/icons-material/Business";
import { useTheme } from "@mui/material/styles";
import useMediaQuery from "@mui/material/useMediaQuery";
import { useMemo, useState } from "react";
import { Outlet, useNavigate, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useEffect } from "react";
import { getHeaderInfo, type HeaderInfo } from "../services/uiService";
import { brandingAssetUrl } from "../utils/brandingUrl";

const drawerWidth = 240;

type MenuItem = {
  text: string;
  icon: React.ReactNode;
  path: string;
  allowedRoles: string[];
};

const MainLayout = () => {
  const { logout, user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const userRole = (user?.role ?? "").toLowerCase();

  const menuItems: MenuItem[] = [
    { text: "Dashboard", icon: <DashboardIcon />, path: "/dashboard", allowedRoles: ["admin", "faculty", "student"] },
    { text: "Students", icon: <PeopleIcon />, path: "/students", allowedRoles: ["admin"] },
    { text: "Attendance", icon: <EventNoteIcon />, path: "/attendance", allowedRoles: ["admin", "faculty"] },
    { text: "Reports", icon: <BarChartIcon />, path: "/reports", allowedRoles: ["admin", "faculty"] },
    { text: "Catalog", icon: <CategoryIcon />, path: "/setup", allowedRoles: ["admin"] },
    { text: "Organization", icon: <BusinessIcon />, path: "/admin-setup", allowedRoles: ["admin"] },
  ];

  const visibleMenuItems = menuItems.filter((item) => item.allowedRoles.includes(userRole));

  const [mobileOpen, setMobileOpen] = useState(false);
  const [header, setHeader] = useState<HeaderInfo | null>(null);

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

  return (
    <Box sx={{ display: "flex", width: "100%", minWidth: 0, minHeight: "100vh", boxSizing: "border-box" }}>
      <CssBaseline />

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
              sx={{ mr: 1, display: { xs: "inline-flex", sm: "none" } }}
            >
              <MenuIcon />
            </IconButton>

            {logoMd && (
              <Box
                component="img"
                src={logoMd}
                srcSet={logoSrcSet}
                sizes="(max-width: 600px) 36px, 44px"
                alt=""
                loading="lazy"
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

          {/* Right side: role + logout */}
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Typography variant="body2" sx={{ display: { xs: "none", sm: "block" } }}>
              {user?.role || "User"}
            </Typography>
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
            top: 64, // below app bar on mobile & desktop (default Toolbar height)
          },
        }}
      >
        <Toolbar />
        <List>
          {visibleMenuItems.map((item) => (
            <ListItemButton
              key={item.text}
              selected={
                item.path === "/setup"
                  ? location.pathname === "/setup" || location.pathname.startsWith("/setup/")
                  : location.pathname === item.path
              }
              onClick={() => {
                navigate(item.path);
                if (isMobile) setMobileOpen(false);
              }}
              sx={{
                "&.Mui-selected": {
                  backgroundColor: "#e3f2fd",
                  color: "#1976d2",
                },
              }}
            >
              <ListItemIcon>{item.icon}</ListItemIcon>
              <ListItemText primary={item.text} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          width: "100%",
          minWidth: 0,
          maxWidth: "100%",
          p: 2,
          pt: 10,
          boxSizing: "border-box",
        }}
      >
        <Outlet />
      </Box>

      <Box
        sx={{
          position: "fixed",
          bottom: 0,
          left: 0,
          right: 0,
          textAlign: "center",
          p: 1,
          backgroundColor: "#f5f5f5",
        }}
      >
        <Typography variant="body2">© Abhyanvaya 2026 - All Rights Reserved</Typography>
      </Box>
    </Box>
  );
};

export default MainLayout;
