export {
  MOBILE_MAX_WIDTH_PX,
  TABLET_MIN_WIDTH_PX,
  TABLET_MAX_WIDTH_PX,
  resolveMobilitySurface,
  isMobileCaptureWidth,
  isTabletReviewWidth,
  safeAreaSx,
  MOBILE_TOUCH_TARGET_PX,
  TABLET_TOUCH_TARGET_PX,
} from "./breakpoints";
export type { MobilitySurface } from "./breakpoints";
export { useMobilitySurface } from "./useMobilitySurface";
export { MobileCaptureLayout } from "./MobileCaptureLayout";
export { OneHandedCaptureChrome, MobileBottomNav } from "./OneHandedCaptureChrome";
export { ClassroomSessionDashboard } from "./ClassroomSessionDashboard";
export type { ClassroomSessionCardModel } from "./ClassroomSessionDashboard";
export { SmartCaptureAssistant, buildCaptureGuidance } from "./SmartCaptureAssistant";
export type { CaptureAssistantHints } from "./SmartCaptureAssistant";
export {
  DOUBLE_TAP_MS,
  DOUBLE_TAP_ZOOM,
  pinchScaleFactor,
  clampScale,
  isDoubleTap,
  createLongPressController,
  touchDistance,
  resolveSwipe,
  LONG_PRESS_MS,
} from "./gestures";
export {
  detectDeviceCapability,
  adaptiveImageQuality,
  shouldVirtualizeList,
} from "./devicePerformance";
export type { DeviceCapabilityProfile } from "./devicePerformance";
export { TabletReviewShell, GestureContextMenu } from "./TabletReviewShell";
export { LazyMediaImage } from "./LazyMediaImage";
export { usePauseHiddenMedia } from "./usePauseHiddenMedia";
export {
  MOBILITY_PHASE2_EXTENSIONS,
  isPhase2CapabilityEnabled,
} from "./extensionPoints";
export type { MobilityPhase2Capability } from "./extensionPoints";
