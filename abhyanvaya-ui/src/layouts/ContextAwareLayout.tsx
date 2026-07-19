import { Stack, type ReactNode } from "@mui/material";
import { useState } from "react";
import OperationalContextBanner from "../components/context/OperationalContextBanner";
import OperationalContextBreadcrumb, { type BreadcrumbItem } from "../components/context/OperationalContextBreadcrumb";
import OperationalContextGuard from "../components/context/OperationalContextGuard";
import CollegeContextSelector from "../components/context/CollegeContextSelector";
import { useTenantContext } from "../context/TenantContextProvider";

type Props = {
  breadcrumbItems: BreadcrumbItem[];
  children: ReactNode;
};

const ContextAwareLayout = ({ breadcrumbItems, children }: Props) => {
  const { refresh } = useTenantContext();
  const [selectorOpen, setSelectorOpen] = useState(false);

  return (
    <Stack spacing={2}>
      <OperationalContextBanner onChangeContext={() => setSelectorOpen(true)} />
      <OperationalContextBreadcrumb items={breadcrumbItems} />
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
