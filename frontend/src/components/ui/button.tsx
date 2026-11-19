import * as React from 'react';
import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import { Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-medium transition-[background-color,border-color,color,box-shadow,transform] duration-150 ease-out focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/30 focus-visible:ring-offset-1 motion-safe:active:translate-y-0 motion-safe:active:scale-[0.98] motion-reduce:transition-colors disabled:pointer-events-none disabled:opacity-50 cursor-pointer [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0',
  {
    variants: {
      variant: {
        success:
          'bg-emerald-700 text-white shadow-sm hover:bg-emerald-800 focus-visible:ring-emerald-600/30 dark:bg-emerald-600 dark:hover:bg-emerald-500',
        soft: 'border border-primary/20 bg-primary/8 text-primary shadow-2xs hover:bg-primary/14 hover:border-primary/30 dark:border-primary/30 dark:bg-primary/15 dark:hover:bg-primary/25',
        dangerOutline:
          'border border-tone-danger-border bg-tone-danger-bg text-tone-danger-text shadow-2xs hover:border-tone-danger-border hover:bg-tone-danger-bg',
        successOutline:
          'border border-tone-success-border bg-tone-success-bg text-tone-success-text shadow-2xs hover:border-tone-success-border hover:bg-tone-success-bg',
        default:
          'bg-brand-solid text-white shadow-[0_1px_2px_rgba(15,23,42,0.08),inset_0_1px_0_rgba(255,255,255,0.16)] hover:bg-brand-solid-hover hover:shadow-[0_6px_16px_-4px_rgba(37,99,235,0.42),0_2px_4px_rgba(15,23,42,0.1),inset_0_1px_0_rgba(255,255,255,0.22)] motion-safe:hover:-translate-y-px active:bg-brand-solid active:shadow-[0_1px_2px_rgba(15,23,42,0.12)]',
        destructive: 'bg-destructive text-destructive-foreground shadow-xs hover:bg-destructive/90',
        outline:
          'border border-line-input/90 bg-surface-card text-ink-secondary shadow-2xs hover:border-line-strong hover:bg-surface-hover/90 hover:text-ink-primary',
        secondary:
          'bg-surface-muted/90 text-ink-heading shadow-2xs hover:bg-surface-strong/75',
        ghost:
          'text-ink-body hover:bg-surface-muted/80 hover:text-ink-primary',
        link: 'text-primary underline-offset-4 hover:underline',
      },
      size: {
        default: 'h-9 px-4 py-2',
        sm: 'h-8 rounded-lg px-3 text-xs',
        lg: 'h-10 rounded-xl px-6',
        icon: 'h-9 w-9',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  },
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  asChild?: boolean;
  isLoading?: boolean;
  loadingText?: string;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      className,
      variant,
      size,
      asChild = false,
      isLoading = false,
      loadingText,
      leftIcon,
      rightIcon,
      disabled,
      children,
      ...props
    },
    ref,
  ) => {
    const Comp = asChild ? Slot : 'button';
    const isDisabled = disabled || isLoading;

    if (asChild) {
      return (
        <Comp className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props}>
          {children}
        </Comp>
      );
    }

    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        disabled={isDisabled}
        aria-busy={isLoading ? 'true' : undefined}
        {...props}
      >
        {isLoading && <Loader2 className="animate-spin" />}
        {!isLoading && leftIcon}
        {isLoading && loadingText ? loadingText : children}
        {!isLoading && rightIcon}
      </Comp>
    );
  },
);
Button.displayName = 'Button';

export { Button, buttonVariants };
