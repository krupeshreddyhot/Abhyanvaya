import {
  Box,
  Chip,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItem,
  ListItemText,
  Stack,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { Link as RouterLink } from "react-router-dom";
import type { SchedulingModuleStatus } from "../../../services/schedulingService";
import { schedulingHubGroups } from "./schedulingCatalogConfig";

type Props = {
  open: boolean;
  onClose: () => void;
  module: SchedulingModuleStatus | null;
};

const titleForKey = (key: string) => {
  for (const g of schedulingHubGroups) {
    const hit = g.items.find((i) => i.key === key);
    if (hit) return hit.title;
  }
  return key;
};

/** AI30.3.5.8 — Help / Dependencies / Used By / Related. */
const ModuleHelpDrawer = ({ open, onClose, module }: Props) => (
  <Drawer
    anchor="right"
    open={open}
    onClose={onClose}
    slotProps={{ paper: { sx: { width: { xs: "100%", sm: 380 } } } }}
  >
    <Box sx={{ p: 2 }}>
      <Stack direction="row" sx={{ mb: 1, alignItems: "center" }}>
        <Typography variant="h6" sx={{ flexGrow: 1 }}>
          {module?.title ?? "Module help"}
        </Typography>
        <IconButton onClick={onClose} aria-label="Close help">
          <CloseIcon />
        </IconButton>
      </Stack>
      {module && (
        <>
          <Chip size="small" label={module.status} sx={{ mb: 1 }} />
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {module.tooltip}
          </Typography>
          <Divider sx={{ my: 1 }} />
          <Typography variant="subtitle2">Requires</Typography>
          <List dense>
            {(module.requires.length ? module.requires : ["None"]).map((r) => (
              <ListItem key={r} disableGutters>
                <ListItemText primary={titleForKey(r)} />
              </ListItem>
            ))}
          </List>
          <Typography variant="subtitle2">Used By</Typography>
          <List dense>
            {(module.usedBy.length ? module.usedBy : ["None"]).map((r) => (
              <ListItem key={r} disableGutters>
                <ListItemText primary={titleForKey(r)} />
              </ListItem>
            ))}
          </List>
          <Typography variant="subtitle2">Related Modules</Typography>
          <List dense>
            {(module.relatedModules.length ? module.relatedModules : ["None"]).map((r) => (
              <ListItem key={r} disableGutters>
                <ListItemText primary={titleForKey(r)} />
              </ListItem>
            ))}
          </List>
          <Stack direction="row" spacing={1} sx={{ mt: 2, flexWrap: "wrap" }}>
            <Chip
              component={RouterLink}
              to={module.path}
              clickable
              label="Open module"
              color="primary"
              variant="outlined"
            />
            {module.helpDocPath && (
              <Chip
                component="a"
                href={module.helpDocPath}
                target="_blank"
                rel="noreferrer"
                clickable
                label="Help doc"
                variant="outlined"
              />
            )}
          </Stack>
        </>
      )}
    </Box>
  </Drawer>
);

export default ModuleHelpDrawer;
