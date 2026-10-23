import { Directive, ElementRef, HostListener, Input, OnChanges, OnDestroy, OnInit, Renderer2, inject } from '@angular/core';

let nextTipId = 1;

/** How long the tooltip waits after the pointer leaves, so it can move onto the tooltip (WCAG 1.4.13). */
export const DISABLED_REASON_HIDE_DELAY_MS = 200;

/**
 * 030 R20 / conventions §7.10 (owner decision 2026-10-14): a control that is unavailable for a
 * reason stays visible and in the Tab order. It gets `aria-disabled="true"` (never `disabled`),
 * its reason becomes its description through `aria-describedby`, and a tooltip shows the reason on
 * hover and on keyboard focus. Esc hides it. Activating the control does nothing while it has a
 * reason. With no reason, the control is left exactly as it was.
 *
 * Usage: `<button class="btn" [appDisabledReason]="markActiveReason()" (click)="markActive()">Mark Active</button>`
 */
@Directive({
  selector: '[appDisabledReason]',
  standalone: true,
})
export class DisabledReasonDirective implements OnInit, OnChanges, OnDestroy {
  /** The reason the control can't be used, or null/empty when it can. */
  @Input('appDisabledReason') reason: string | null | undefined;

  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private renderer = inject(Renderer2);

  private tip: HTMLElement | null = null;
  private readonly tipId = `disabled-reason-${nextTipId++}`;
  private overHost = false;
  private overTip = false;
  private unlistenTip: (() => void)[] = [];

  /** Capture phase, so the control's own (click) handlers never run while it has a reason. */
  private readonly blockClick = (event: Event) => {
    if (!this.active) return;
    event.preventDefault();
    event.stopImmediatePropagation();
  };

  private get active(): boolean {
    return !!this.reason && this.reason.trim().length > 0;
  }

  ngOnInit(): void {
    this.host.nativeElement.addEventListener('click', this.blockClick, true);
  }

  ngOnChanges(): void {
    if (this.active) this.attach(this.reason!.trim());
    else this.detach();
  }

  ngOnDestroy(): void {
    this.host.nativeElement.removeEventListener('click', this.blockClick, true);
    this.detach();
  }

  @HostListener('mouseenter')
  onMouseEnter(): void {
    this.overHost = true;
    this.show();
  }

  @HostListener('mouseleave')
  onMouseLeave(): void {
    this.overHost = false;
    this.hideUnlessHovered();
  }

  @HostListener('focus')
  onFocus(): void {
    this.show();
  }

  @HostListener('blur')
  onBlur(): void {
    this.hide();
  }

  @HostListener('keydown.escape')
  onEscape(): void {
    this.hide();
  }

  private attach(reason: string): void {
    const el = this.host.nativeElement;
    if (!this.tip) {
      const tip = this.renderer.createElement('span') as HTMLElement;
      this.renderer.setAttribute(tip, 'id', this.tipId);
      this.renderer.setAttribute(tip, 'role', 'tooltip');
      this.renderer.addClass(tip, 'disabled-reason-tip');
      tip.hidden = true;
      this.unlistenTip = [
        this.renderer.listen(tip, 'mouseenter', () => { this.overTip = true; }),
        this.renderer.listen(tip, 'mouseleave', () => { this.overTip = false; this.hideUnlessHovered(); }),
      ];
      const parent = this.renderer.parentNode(el);
      if (parent) this.renderer.insertBefore(parent, tip, this.renderer.nextSibling(el));
      this.tip = tip;
    }
    this.tip.textContent = reason;
    this.renderer.setAttribute(el, 'aria-disabled', 'true');
    this.setDescribedBy(true);
  }

  private detach(): void {
    const el = this.host.nativeElement;
    if (!this.tip) return;
    this.unlistenTip.forEach(un => un());
    this.unlistenTip = [];
    this.tip.remove();
    this.tip = null;
    this.renderer.removeAttribute(el, 'aria-disabled');
    this.setDescribedBy(false);
  }

  private setDescribedBy(add: boolean): void {
    const el = this.host.nativeElement;
    const ids = (el.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(id => id && id !== this.tipId);
    if (add) ids.push(this.tipId);
    if (ids.length) this.renderer.setAttribute(el, 'aria-describedby', ids.join(' '));
    else this.renderer.removeAttribute(el, 'aria-describedby');
  }

  private show(): void {
    if (!this.tip || !this.active) return;
    // Fixed positioning under the control, so no ancestor needs to be positioned.
    const rect = this.host.nativeElement.getBoundingClientRect();
    this.renderer.setStyle(this.tip, 'top', `${Math.round(rect.bottom + 2)}px`);
    this.renderer.setStyle(this.tip, 'left', `${Math.round(Math.max(8, rect.left))}px`);
    this.tip.hidden = false;
  }

  private hide(): void {
    if (this.tip) this.tip.hidden = true;
  }

  private hideUnlessHovered(): void {
    // Let the pointer cross from the control onto the tooltip (WCAG 1.4.13 "hoverable").
    setTimeout(() => {
      if (!this.overHost && !this.overTip) this.hide();
    }, DISABLED_REASON_HIDE_DELAY_MS);
  }
}
