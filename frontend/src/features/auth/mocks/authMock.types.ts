import type { OtpMethod, OtpPurpose } from '../types/auth.types';

export interface MockStudentProfile {
  id: string;
  name: string;
  email: string;
  phone: string;
}

// Local simulation state, never an API response or proof of authentication.
export interface MockOtpChallenge {
  purpose: OtpPurpose;
  method: OtpMethod;
  expires: number;
  resendAt: number;
  attempts: number;
  verified: boolean;
  pendingPassword?: string;
}
