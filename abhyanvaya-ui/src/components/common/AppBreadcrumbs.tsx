import { Link as RouterLink } from "react-router-dom";
import { Breadcrumbs, Link, Typography } from "@mui/material";

export type BreadcrumbItem = {
  label: string;
  /** Omit for the current page — rendered as plain (non-clickable) text. */
  to?: string;
};

export type AppBreadcrumbsProps = {
  items: BreadcrumbItem[];
};

/**
 * Generic clickable breadcrumb trail (AI20.UI.3). Deliberately data-driven — any page adds another
 * level (e.g. AI Center › Student Enrollment › Batch #1234) by extending its `items` array, with no
 * changes to this component.
 */
const AppBreadcrumbs = ({ items }: AppBreadcrumbsProps) => (
  <Breadcrumbs aria-label="breadcrumb">
    {items.map((item) =>
      item.to ? (
        <Link key={item.label} component={RouterLink} to={item.to} underline="hover" color="inherit">
          {item.label}
        </Link>
      ) : (
        <Typography key={item.label} color="text.primary">
          {item.label}
        </Typography>
      ),
    )}
  </Breadcrumbs>
);

export default AppBreadcrumbs;
