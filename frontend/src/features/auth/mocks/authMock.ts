import {
  MAX_OTP_ATTEMPTS,
  OTP_EXPIRY_MS,
  OTP_RESEND_COOLDOWN_MS,
} from '../constants/auth.constants';
import { DEMO_OTP, DEMO_PASSWORD, DEMO_PROFILE } from './authFixtures';
import type { OtpMethod, OtpPurpose } from '../types/auth.types';
import type { MockOtpChallenge, MockStudentProfile } from './authMock.types';
import { isStrongPassword } from '../utils/passwordValidation';

// In-memory demo service. Replace these functions with HTTP calls when the API is ready.
let currentPassword: string = DEMO_PASSWORD;
let challenge: MockOtpChallenge | null = null;

export function matchesProfile(identifier: string): boolean {
  const normalizedIdentifier = identifier.trim().toLowerCase();
  return normalizedIdentifier === DEMO_PROFILE.id || normalizedIdentifier === DEMO_PROFILE.email;
}

export function findEligibleStudentMock(studentId: string): MockStudentProfile {
  const normalizedStudentId = studentId.trim();

  if (!/^\d{8}$/.test(normalizedStudentId)) {
    throw new Error('MSSV phải gồm 8 chữ số.');
  }

  if (normalizedStudentId !== DEMO_PROFILE.id) {
    throw new Error('Không tìm thấy hồ sơ đủ điều kiện kích hoạt với MSSV này.');
  }

  return DEMO_PROFILE;
}

export async function loginMock(identifier: string, password: string): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 500));

  if (!matchesProfile(identifier) || password !== currentPassword) {
    throw new Error('MSSV/email hoặc mật khẩu không đúng.');
  }
}

export function startChallenge(
  purpose: OtpPurpose,
  method: OtpMethod,
  pendingPassword?: string,
): void {
  const now = Date.now();
  challenge = {
    purpose,
    method,
    expires: now + OTP_EXPIRY_MS,
    resendAt: now + OTP_RESEND_COOLDOWN_MS,
    attempts: 0,
    verified: false,
    pendingPassword,
  };
}

export function getChallenge(): MockOtpChallenge | null {
  return challenge;
}

export function resendChallenge(): boolean {
  if (!challenge || Date.now() < challenge.resendAt || challenge.attempts >= MAX_OTP_ATTEMPTS) {
    return false;
  }

  startChallenge(challenge.purpose, challenge.method, challenge.pendingPassword);
  return true;
}

export function verifyOtp(value: string): OtpPurpose {
  if (!challenge) {
    throw new Error('Vui lòng yêu cầu mã OTP trước.');
  }

  if (challenge.attempts >= MAX_OTP_ATTEMPTS) {
    throw new Error('Đã sai 5 lần. Luồng demo đã khóa; quay lại để bắt đầu lại.');
  }

  if (Date.now() >= challenge.expires) {
    throw new Error('Mã đã hết hạn. Vui lòng gửi lại OTP.');
  }

  if (value !== DEMO_OTP) {
    challenge.attempts += 1;
    throw new Error(
      challenge.attempts >= MAX_OTP_ATTEMPTS
        ? 'Đã sai 5 lần. Quay lại để bắt đầu lại.'
        : `Mã OTP không đúng. Còn ${MAX_OTP_ATTEMPTS - challenge.attempts} lần thử.`,
    );
  }

  challenge.verified = true;

  if (challenge.purpose === 'activate' && challenge.pendingPassword) {
    currentPassword = challenge.pendingPassword;
  }

  return challenge.purpose;
}

export function resetPasswordMock(password: string): void {
  if (!challenge?.verified || challenge.purpose !== 'reset' || Date.now() >= challenge.expires) {
    throw new Error('Phiên xác thực đã hết hạn. Vui lòng yêu cầu OTP mới.');
  }

  if (!isStrongPassword(password)) {
    throw new Error('Mật khẩu chưa đạt yêu cầu.');
  }

  currentPassword = password;
  challenge = null;
}
