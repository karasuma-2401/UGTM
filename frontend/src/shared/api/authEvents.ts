type AuthEventListener = () => void;

class AuthEventEmitter {
  private unauthenticatedListeners: Set<AuthEventListener> = new Set();

  onUnauthenticated(listener: AuthEventListener): () => void {
    this.unauthenticatedListeners.add(listener);
    return () => {
      this.unauthenticatedListeners.delete(listener);
    };
  }

  emitUnauthenticated(): void {
    this.unauthenticatedListeners.forEach((listener) => {
      try {
        listener();
      } catch (err) {
        console.error('Error in unauthenticated event listener:', err);
      }
    });
  }
}

export const authEvents = new AuthEventEmitter();
