import type { Pose } from '$shell/olli/poses';

/** How a scene's demo gets Olli to answer what just happened: a new pose, and what Olli says now. */
export type React = (pose: Pose, say: string) => void;
