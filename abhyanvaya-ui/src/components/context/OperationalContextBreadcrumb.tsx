import { Breadcrumbs, Link, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { useTenantContext } from "../../context/TenantContextProvider";

export type BreadcrumbItem = {
  label: string;
  to?: string;
};

type Props = {
  items: BreadcrumbItem[];
};

const OperationalContextBreadcrumb = ({ items }: Props) => {
  const { context } = useTenantContext();

  const trail: BreadcrumbItem[] = [
    { label: "Home", to: "/dashboard" },
    ...items,
  ];

  if (context?.selectedCollegeName) {
    trail.push({ label: context.selectedCollegeName });
  }

  return (
    <Breadcrumbs aria-label="Operational context breadcrumb">
      {trail.map((item, index) => {
        const isLast = index === trail.length - 1;
        if (isLast || !item.to) {
          return (
            <Typography key={`${item.label}-${index}`} color="text.primary" variant="body2">
              {item.label}
            </Typography>
          );
        }

        return (
          <Link key={`${item.label}-${index}`} component={RouterLink} to={item.to} underline="hover" color="inherit" variant="body2">
            {item.label}
          </Link>
        );
      })}
    </Breadcrumbs>
  );
};

export default OperationalContextBreadcrumb;
