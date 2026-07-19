import { Box, Typography } from "@mui/material";
import { useEffect, useState, type ReactNode } from "react";
import CollegeContextSelector from "./CollegeContextSelector";
import { useTenantContext } from "../../context/TenantContextProvider";

type Props = {
  children: ReactNode;
};

const OperationalContextGuard = ({ children }: Props) => {
  const { isSuperAdmin, hasOperationalContext, refresh } = useTenantContext();
  const [selectorOpen, setSelectorOpen] = useState(false);

  useEffect(() => {
    if (isSuperAdmin && !hasOperationalContext) {
      setSelectorOpen(true);
    }
  }, [isSuperAdmin, hasOperationalContext]);

  if (!isSuperAdmin || hasOperationalContext) {
    return <>{children}</>;
  }

  return (
    <>
      <Box sx={{ py: 4, textAlign: "center" }}>
        <Typography variant="body1" color="text.secondary">
          Select an operational college context to continue. Your login session remains active.
        </Typography>
      </Box>
      <CollegeContextSelector
        open={selectorOpen}
        onSelected={() => {
          setSelectorOpen(false);
          void refresh();
        }}
      />
    </>
  );
};

export default OperationalContextGuard;
