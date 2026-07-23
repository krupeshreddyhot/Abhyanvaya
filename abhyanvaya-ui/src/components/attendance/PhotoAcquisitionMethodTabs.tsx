import { Tab, Tabs, Typography, Stack } from "@mui/material";
import {
  PHOTO_ACQUISITION_METHODS,
  type PhotoAcquisitionMethod,
} from "../../types/photoAcquisition";

export type PhotoAcquisitionMethodTabsProps = {
  value: PhotoAcquisitionMethod;
  onChange: (method: PhotoAcquisitionMethod) => void;
  disabled?: boolean;
};

export const PhotoAcquisitionMethodTabs = ({
  value,
  onChange,
  disabled = false,
}: PhotoAcquisitionMethodTabsProps) => {
  const current = PHOTO_ACQUISITION_METHODS.find((method) => method.id === value);

  return (
    <Stack spacing={1}>
      <Tabs
        value={value}
        onChange={(_, next: PhotoAcquisitionMethod) => onChange(next)}
        variant="scrollable"
        allowScrollButtonsMobile
        aria-label="Classroom photo acquisition method"
      >
        {PHOTO_ACQUISITION_METHODS.map((method) => (
          <Tab
            key={method.id}
            value={method.id}
            label={method.label}
            disabled={disabled}
            id={`photo-acq-tab-${method.id}`}
            aria-controls={`photo-acq-panel-${method.id}`}
          />
        ))}
      </Tabs>
      {current && (
        <Typography variant="body2" color="text.secondary">
          {current.description}
        </Typography>
      )}
    </Stack>
  );
};

export default PhotoAcquisitionMethodTabs;
