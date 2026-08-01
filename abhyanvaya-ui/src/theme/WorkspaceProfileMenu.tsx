import DensityMediumIcon from "@mui/icons-material/DensityMedium";
import {
  IconButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Tooltip,
} from "@mui/material";
import { useState } from "react";
import { useThemeManager } from "./ThemeManager";
import type { WorkspaceProfileId } from "./workspacePersonalization";

const PROFILES: { id: WorkspaceProfileId; label: string }[] = [
  { id: "compact", label: "Compact" },
  { id: "standard", label: "Standard" },
  { id: "largeMonitor", label: "Large monitor" },
  { id: "touch", label: "Touch / Tablet" },
];

/** AI22.7B Phase 5.8 — workspace density / profile personalization. */
export function WorkspaceProfileMenu() {
  const { prefs, setProfile } = useThemeManager();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);

  return (
    <>
      <Tooltip title={`Workspace profile: ${prefs.profile}`}>
        <IconButton
          color="inherit"
          size="small"
          onClick={(event) => setAnchor(event.currentTarget)}
          aria-label="Workspace profile"
          aria-haspopup="menu"
        >
          <DensityMediumIcon fontSize="small" />
        </IconButton>
      </Tooltip>
      <Menu
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={() => setAnchor(null)}
        slotProps={{ list: { "aria-label": "Workspace profiles" } }}
      >
        {PROFILES.map((profile) => (
          <MenuItem
            key={profile.id}
            selected={prefs.profile === profile.id}
            onClick={() => {
              setProfile(profile.id);
              setAnchor(null);
            }}
          >
            <ListItemIcon>
              <DensityMediumIcon fontSize="small" />
            </ListItemIcon>
            <ListItemText>{profile.label}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}

export default WorkspaceProfileMenu;
