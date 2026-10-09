interface AuthErrorMessageProps {
  message: string;
}

export function AuthErrorMessage({ message }: AuthErrorMessageProps) {
  if (!message) return null;

  return (
    <p className="auth-error" role="alert">
      {message}
    </p>
  );
}
