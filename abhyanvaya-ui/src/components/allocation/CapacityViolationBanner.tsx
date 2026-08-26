import { Alert, Chip, Stack, Typography } from "@mui/material";
import {
  extractCapacityViolations,
  proposedOverCapacitySections,
  type EngineConstraintEval,
} from "../../utils/allocationCapacityViolations";
import { priorityDisplayLabel } from "../../utils/allocationAdministratorCopy";
import { constraintLabel } from "../../utils/allocationStrategyCatalog";

type Props = {
  constraints?: readonly EngineConstraintEval[] | null;
  proposedSummaries?: readonly {
    sectionId: number;
    sectionCode: string;
    assignedCount: number;
    maximumCapacity: number;
    occupancyPercent?: number;
  }[] | null;
};

/**
 * Surfaces capacity issues with administrator labels (Required / Preferred).
 * Does not commit or recalculate capacity.
 */
const CapacityViolationBanner = ({ constraints, proposedSummaries }: Props) => {
  const violations = extractCapacityViolations(constraints);
  const proposedOver = proposedOverCapacitySections(proposedSummaries);

  if (!violations.length && !proposedOver.length) return null;

  return (
    <Stack spacing={1}>
      {violations.map((v) => (
        <Alert key={`${v.constraintCode}-${v.priority}`} severity={v.isMandatory ? "error" : "warning"}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center", mb: 0.5 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Capacity issue — {constraintLabel(v.constraintCode)}
            </Typography>
            <Chip
              size="small"
              color={v.isMandatory ? "error" : v.isPreferred ? "warning" : "default"}
              label={priorityDisplayLabel(v.priority)}
            />
          </Stack>
          {v.summary}
          {v.isMandatory && (
            <Typography variant="body2" sx={{ mt: 0.5 }}>
              Required — resolve this capacity issue before approving the allocation.
            </Typography>
          )}
          {v.isPreferred && !v.isMandatory && (
            <Typography variant="body2" sx={{ mt: 0.5 }}>
              Preferred — review before proceeding.
            </Typography>
          )}
        </Alert>
      ))}

      {proposedOver.length > 0 && (
        <Alert severity="error">
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
            Proposed allocation exceeds section capacity
          </Typography>
          {proposedOver.map((s) => (
            <Typography key={s.sectionCode} variant="body2">
              {s.sectionCode}: assigned {s.assignedCount} / capacity {s.maximumCapacity}
              {s.occupancyPercent != null ? ` (${s.occupancyPercent}%)` : ""}
            </Typography>
          ))}
        </Alert>
      )}
    </Stack>
  );
};

export default CapacityViolationBanner;
