import ContrastIcon from "@mui/icons-material/Contrast";
import DarkModeOutlinedIcon from "@mui/icons-material/DarkModeOutlined";
import LightModeOutlinedIcon from "@mui/icons-material/LightModeOutlined";
import SettingsBrightnessIcon from "@mui/icons-material/SettingsBrightness";
import { IconButton, Menu, MenuItem, ListItemIcon, ListItemText, Tooltip } from "@mui/material";
import { useState, type ReactNode } from "react";
import type { ThemeModePreference } from "./enterpriseTokens";
import { useThemeManager } from "./ThemeManager";

const MODE_META: Record<
  ThemeModePreference,
  { label: string; icon: ReactNode }
> = {
  light: { label: "Light", icon: <LightModeOutlinedIcon fontSize="small" /> },
  dark: { label: "Dark", icon: <DarkModeOutlinedIcon fontSize="small" /> },
  system: { label: "System", icon: <SettingsBrightnessIcon fontSize="small" /> },
  highContrast: { label: "High Contrast", icon: <ContrastIcon fontSize="small" /> },
};

/** AI22.7B-R2 — Appearance menu: Light / Dark / System / High Contrast (single active). */
export function ThemeModeToggle() {
  const { themeMode, setThemeMode, resolvedScheme } = useThemeManager();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  return (
    <>
      <Tooltip title={`Appearance: ${MODE_META[themeMode].label} (${resolvedScheme})`}>
        <IconButton
          color="inherit"
          size="small"
          onClick={(event) => setAnchor(event.currentTarget)}
          aria-label="Appearance menu"
          aria-haspopup="menu"
          aria-expanded={Boolean(anchor)}
        >
          {MODE_META[themeMode].icon}
        </IconButton>
      </Tooltip>
      <Menu
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={() => setAnchor(null)}
        slotProps={{ list: { "aria-label": "Appearance options" } }}
      >
        {(Object.keys(MODE_META) as ThemeModePreference[]).map((mode) => (
          <MenuItem
            key={mode}
            selected={themeMode === mode}
            onClick={() => {
              setThemeMode(mode);
              setAnchor(null);
            }}
          >
            <ListItemIcon>{MODE_META[mode].icon}</ListItemIcon>
            <ListItemText>{MODE_META[mode].label}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}

export default ThemeModeToggle;
