import { Box } from "@mui/material";
import { memo, useCallback, useMemo, useRef, useState, type ReactNode } from "react";

type VirtualizedRecognitionListProps<T> = {
  items: T[];
  itemHeight: number;
  height: number;
  getKey: (item: T) => string;
  renderItem: (item: T) => ReactNode;
};

function VirtualizedRecognitionListInner<T>({
  items,
  itemHeight,
  height,
  getKey,
  renderItem,
}: VirtualizedRecognitionListProps<T>) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [scrollTop, setScrollTop] = useState(0);

  const onScroll = useCallback(() => {
    if (scrollRef.current) {
      setScrollTop(scrollRef.current.scrollTop);
    }
  }, []);

  const { startIndex, endIndex, offsetY, totalHeight } = useMemo(() => {
    const overscan = 2;
    const start = Math.max(0, Math.floor(scrollTop / itemHeight) - overscan);
    const visibleCount = Math.ceil(height / itemHeight) + overscan * 2;
    const end = Math.min(items.length, start + visibleCount);
    return {
      startIndex: start,
      endIndex: end,
      offsetY: start * itemHeight,
      totalHeight: items.length * itemHeight,
    };
  }, [scrollTop, itemHeight, height, items.length]);

  const visibleItems = items.slice(startIndex, endIndex);

  return (
    <Box
      ref={scrollRef}
      onScroll={onScroll}
      role="list"
      aria-label="Recognition list"
      sx={{
        height,
        overflowY: "auto",
        position: "relative",
        pr: 0.5,
      }}
    >
      <Box sx={{ height: totalHeight, position: "relative" }}>
        <Box sx={{ transform: `translateY(${offsetY}px)` }}>
          {visibleItems.map((item) => (
            <Box key={getKey(item)} role="listitem" sx={{ height: itemHeight, pb: 1 }}>
              {renderItem(item)}
            </Box>
          ))}
        </Box>
      </Box>
    </Box>
  );
}

export const VirtualizedRecognitionList = memo(
  VirtualizedRecognitionListInner
) as typeof VirtualizedRecognitionListInner;
