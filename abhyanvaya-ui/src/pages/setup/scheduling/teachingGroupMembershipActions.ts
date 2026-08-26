import { getApiErrorMessage, getHttpStatus } from "../../../utils/apiErrorMessage";
import {
  addTeachingGroupMembers,
  getResolvedTeachingGroupMembers,
  getTeachingGroup,
  getTeachingGroupMemberships,
  removeTeachingGroupMember,
  type ResolvedTeachingGroupMemberDto,
  type TeachingGroupDetailDto,
  type TeachingGroupMembershipDto,
} from "../../../services/teachingGroupService";
import { MEMBERSHIP_CONFLICT_MESSAGE, uniqueStudentIds } from "./teachingGroupMembershipUi";

export type MembershipAuthoritativeState = {
  detail: TeachingGroupDetailDto;
  memberships: TeachingGroupMembershipDto[];
  resolvedMembers: ResolvedTeachingGroupMemberDto[];
};

export type MembershipMutationOutcome =
  | { kind: "success"; state: MembershipAuthoritativeState; message: string }
  | { kind: "conflict"; state: MembershipAuthoritativeState; message: string }
  | { kind: "error"; message: string; status?: number };

/** Reload Teaching Group + overlays + resolved roster from the server (authoritative). */
export const reloadTeachingGroupMembershipState = async (
  teachingGroupId: number,
): Promise<MembershipAuthoritativeState> => {
  const [detailRes, membershipRes, resolvedRes] = await Promise.all([
    getTeachingGroup(teachingGroupId),
    getTeachingGroupMemberships(teachingGroupId),
    getResolvedTeachingGroupMembers(teachingGroupId),
  ]);
  return {
    detail: detailRes.data,
    memberships: membershipRes.data,
    resolvedMembers: resolvedRes.data,
  };
};

const conflictOrError = async (
  teachingGroupId: number,
  error: unknown,
): Promise<MembershipMutationOutcome> => {
  const status = getHttpStatus(error);
  if (status === 409) {
    try {
      const state = await reloadTeachingGroupMembershipState(teachingGroupId);
      return { kind: "conflict", state, message: MEMBERSHIP_CONFLICT_MESSAGE };
    } catch {
      return {
        kind: "error",
        status: 409,
        message: MEMBERSHIP_CONFLICT_MESSAGE,
      };
    }
  }
  return {
    kind: "error",
    status,
    message: getApiErrorMessage(error, "Membership update failed.", {
      forbiddenFallback:
        "You are not authorized to manage Teaching Group membership. Ask an administrator if you need Scheduling.TeachingGroup.Manage.",
    }),
  };
};

/**
 * Add explicit include overlays via POST, then reload authoritative state.
 * Does not invent a resolved roster from the mutation response alone.
 * Does not auto-retry on 409.
 */
export const addTeachingGroupMembersWithReload = async (
  teachingGroupId: number,
  studentIds: number[],
): Promise<MembershipMutationOutcome> => {
  const ids = uniqueStudentIds(studentIds);
  if (ids.length === 0) {
    return { kind: "error", message: "Select at least one student to add." };
  }
  try {
    await addTeachingGroupMembers(teachingGroupId, { studentIds: ids });
    const state = await reloadTeachingGroupMembershipState(teachingGroupId);
    return {
      kind: "success",
      state,
      message:
        ids.length === 1
          ? "Student added to Teaching Group membership."
          : `${ids.length} students added to Teaching Group membership.`,
    };
  } catch (error) {
    return conflictOrError(teachingGroupId, error);
  }
};

/**
 * Remove one member via DELETE, then reload authoritative state.
 * Does not auto-retry on 409.
 */
export const removeTeachingGroupMemberWithReload = async (
  teachingGroupId: number,
  studentId: number,
): Promise<MembershipMutationOutcome> => {
  try {
    await removeTeachingGroupMember(teachingGroupId, studentId);
    const state = await reloadTeachingGroupMembershipState(teachingGroupId);
    return {
      kind: "success",
      state,
      message: "Student removed from Teaching Group membership.",
    };
  } catch (error) {
    return conflictOrError(teachingGroupId, error);
  }
};
