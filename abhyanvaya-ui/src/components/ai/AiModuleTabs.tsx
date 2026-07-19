import { Tab, Tabs, Tooltip } from "@mui/material";

export type AiModuleTabDef = {
  value: string;
  label: string;
  disabled?: boolean;
  /** Tooltip shown while hovering a disabled tab (e.g. "Available in future phase."). */
  disabledReason?: string;
};

export type AiModuleTabsProps = {
  tabs: AiModuleTabDef[];
  value: string;
  onChange: (value: string) => void;
};

/**
 * Reusable tab-bar shell for AI module dashboards (AI20.UI.4) — the Overview/History/Failures/
 * Settings shape is expected to repeat across future AI modules, so the tab list itself is a prop,
 * not hardcoded here. Disabled tabs are wrapped in a `<span>` inside the `Tooltip` because MUI does
 * not forward pointer events (and therefore never fires the Tooltip trigger) to a disabled button.
 */
const AiModuleTabs = ({ tabs, value, onChange }: AiModuleTabsProps) => (
  <Tabs
    value={value}
    onChange={(_, newValue: string) => onChange(newValue)}
    variant="scrollable"
    scrollButtons="auto"
    sx={{ borderBottom: 1, borderColor: "divider" }}
  >
    {tabs.map((tab) =>
      tab.disabled ? (
        <Tooltip key={tab.value} title={tab.disabledReason ?? "Not available yet."}>
          <span>
            <Tab value={tab.value} label={tab.label} disabled />
          </span>
        </Tooltip>
      ) : (
        <Tab key={tab.value} value={tab.value} label={tab.label} />
      ),
    )}
  </Tabs>
);

export default AiModuleTabs;
