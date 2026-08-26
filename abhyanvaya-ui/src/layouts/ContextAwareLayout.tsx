import { Stack } from "@mui/material";
import { useState, type ReactNode } from "react";
import { AcademicContextBreadcrumb } from "../components/academic";
import OperationalContextBanner from "../components/context/OperationalContextBanner";
import OperationalContextBreadcrumb, { type BreadcrumbItem } from "../components/context/OperationalContextBreadcrumb";
import OperationalContextGuard from "../components/context/OperationalContextGuard";
import CollegeContextSelector from "../components/context/CollegeContextSelector";
import { useTenantContext } from "../context/TenantContextProvider";
import type { AcademicOperationalContextQuery } from "../services/academicBreadcrumbService";

type Props = {
  breadcrumbItems: BreadcrumbItem[];
  children: ReactNode;
  /** AI29.1D Prompt 16 — show shared academic context trail from breadcrumb API. */
  showAcademicContext?: boolean;
  academicContextOverride?: AcademicOperationalContextQuery | null;
};

const ContextAwareLayout = ({
  breadcrumbItems,
  children,
  showAcademicContext = false,
  academicContextOverride = null,
}: Props) => {
  const { refresh } = useTenantContext();
  const [selectorOpen, setSelectorOpen] = useState(false);

  return (
    <Stack spacing={2}>
      <OperationalContextBanner onChangeContext={() => setSelectorOpen(true)} />
      <OperationalContextBreadcrumb items={breadcrumbItems} />
      {showAcademicContext ? <AcademicContextBreadcrumb context={academicContextOverride} /> : null}
      <OperationalContextGuard>{children}</OperationalContextGuard>
      <CollegeContextSelector
        open={selectorOpen}
        onSelected={() => {
          setSelectorOpen(false);
          void refresh();
        }}
      />
    </Stack>
  );
};

export default ContextAwareLayout;
