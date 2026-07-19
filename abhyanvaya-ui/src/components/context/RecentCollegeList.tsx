import { Box, Card, CardActionArea, CardContent, Stack, Typography } from "@mui/material";
import PushPinOutlinedIcon from "@mui/icons-material/PushPinOutlined";
import StarBorderIcon from "@mui/icons-material/StarBorder";
import type { AvailableCollegeDto, RecentCollegeEntry } from "../../types/tenantContext";

type Props = {
  recent: RecentCollegeEntry[];
  popular: AvailableCollegeDto[];
  onSelect: (collegeId: number) => void;
  selecting?: boolean;
};

const RecentCollegeList = ({ recent, popular, onSelect, selecting }: Props) => {
  if (recent.length === 0 && popular.length === 0) {
    return null;
  }

  const title = recent.length > 0 ? "Recent Colleges" : "Popular Colleges";
  const items = recent.length > 0 ? recent : popular;

  return (
    <Box sx={{ mb: 2 }}>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>
        {title}
      </Typography>
      <Stack spacing={1}>
        {items.map((item) => {
          const collegeId = "collegeId" in item ? item.collegeId : item.id;
          const name = item.name;
          const code = item.code;
          const pinned = "isPinned" in item && item.isPinned;
          const favorite = "isFavorite" in item && item.isFavorite;

          return (
            <Card key={collegeId} variant="outlined">
              <CardActionArea onClick={() => onSelect(collegeId)} disabled={selecting}>
                <CardContent sx={{ py: 1.25, "&:last-child": { pb: 1.25 } }}>
                  <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                    <Box sx={{ flex: 1 }}>
                      <Typography variant="body2">{name}</Typography>
                      <Typography variant="caption" color="text.secondary">
                        {code}
                      </Typography>
                    </Box>
                    {pinned ? <PushPinOutlinedIcon fontSize="small" color="action" aria-label="Pinned (future)" /> : null}
                    {favorite ? <StarBorderIcon fontSize="small" color="action" aria-label="Favorite (future)" /> : null}
                  </Stack>
                </CardContent>
              </CardActionArea>
            </Card>
          );
        })}
      </Stack>
    </Box>
  );
};

export default RecentCollegeList;
