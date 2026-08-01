import { Box } from "@mui/material";
import {
  memo,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";

type VirtualizedRecognitionListProps<T> = {
  items: T[];
  itemHeight: number;
  height: number;
  getKey: (item: T) => string;
  renderItem: (item: T) => ReactNode;
  /** Scroll this key into view when it changes (Phase 4.2 face↔list sync). */
  scrollToKey?: string | null;
};

function VirtualizedRecognitionListInner<T>({
  items,
  itemHeight,
  height,
  getKey,
  renderItem,
  scrollToKey,
}: VirtualizedRecognitionListProps<T>) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [scrollTop, setScrollTop] = useState(0);

  const onScroll = useCallback(() => {
    if (scrollRef.current) {
      setScrollTop(scrollRef.current.scrollTop);
    }
  }, []);

  const keyIndex = useMemo(() => {
    if (!scrollToKey) {
      return -1;
    }
    return items.findIndex((item) => getKey(item) === scrollToKey);
  }, [items, getKey, scrollToKey]);

  useEffect(() => {
    if (keyIndex < 0 || !scrollRef.current) {
      return;
    }
    const top = keyIndex * itemHeight;
    const viewTop = scrollRef.current.scrollTop;
    const viewBottom = viewTop + height;
    if (top < viewTop || top + itemHeight > viewBottom) {
      scrollRef.current.scrollTo({
        top: Math.max(0, top - itemHeight),
        behavior: "smooth",
      });
    }
  }, [keyIndex, itemHeight, height]);

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
        scrollBehavior: "smooth",
        "@media (prefers-reduced-motion: reduce)": {
          scrollBehavior: "auto",
        },
      }}
    >
      <Box sx={{ height: totalHeight, position: "relative" }}>
        <Box sx={{ transform: `translateY(${offsetY}px)` }}>
          {visibleItems.map((item) => {
            const key = getKey(item);
            const focused = scrollToKey === key;
            return (
              <Box
                key={key}
                role="listitem"
                data-recognition-id={key}
                sx={{
                  height: itemHeight,
                  pb: 1,
                  transition: (theme) =>
                    theme.transitions.create("background-color", {
                      duration: theme.transitions.duration.shortest,
                    }),
                  bgcolor: focused ? "action.selected" : "transparent",
                  borderRadius: 1,
                }}
              >
                {renderItem(item)}
              </Box>
            );
          })}
        </Box>
      </Box>
    </Box>
  );
}

export const VirtualizedRecognitionList = memo(
  VirtualizedRecognitionListInner,
) as typeof VirtualizedRecognitionListInner;
