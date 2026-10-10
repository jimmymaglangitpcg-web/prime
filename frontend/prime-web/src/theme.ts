import type { ThemeConfig } from 'antd';

/**
 * PRIME's colours and type (docs/analysis/ui-theme.md §4.4–§4.5, decided 2026-10-10). One place for the palette: Ant Design
 * reads it as theme tokens, and the few hand-drawn parts (logo, sign-in panel) import it from here. Every text pairing keeps
 * WCAG 2.1 AA (production-hardening.md Q10); the ratios are noted beside each colour.
 */
export const brand = {
  /** Sidebar, logo tile, sign-in panel; white text on it 15.4:1. */
  navy: '#10263D',
  /** An open sidebar group's background. */
  navyDeep: '#0B1B2C',
  /** Buttons, links, selected entry, chart bars; 6.9:1 on white. */
  primary: '#1F5C99',
  /** Attention only (counts waiting, the logo's monument dot); never text on white (2.1:1). Navy text on it 7.4:1. */
  amber: '#F2A33A',
  /** Body text; 15:1 on mist. */
  ink: '#17212B',
  /** Secondary text; 7.6:1 on white, 7.1:1 on mist. */
  slate: '#475563',
  /** Tertiary text and placeholders; 5.9:1 on white, 5.4:1 on mist. */
  slateLight: '#5B6672',
  /** Page background behind cards. */
  mist: '#F4F6F8',
  /** Error text (Typography danger, form errors); 5.5:1 on white, 5.1:1 on mist. Ant Design's default red is 3.3:1. */
  error: '#C0362C',
  /** Warning text; 6.3:1 on white. Ant Design's default gold is about 2:1. */
  warningText: '#8A5300',
  /** Success text; 5.6:1 on white. */
  successText: '#237804',
  /** Sidebar entry text; 10:1 on navy. */
  sidebarText: '#C9D3DE',
} as const;

export const fontSans = "'IBM Plex Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
export const fontMono = "'IBM Plex Mono', ui-monospace, SFMono-Regular, Consolas, monospace";

export const primeTheme: ThemeConfig = {
  token: {
    colorPrimary: brand.primary,
    colorLink: brand.primary,
    colorInfo: brand.primary,
    colorText: brand.ink,
    colorTextHeading: brand.ink,
    colorTextSecondary: brand.slate,
    colorTextDescription: brand.slate,
    colorTextTertiary: brand.slateLight,
    colorTextPlaceholder: brand.slateLight,
    colorBgLayout: brand.mist,
    colorError: brand.error,
    colorErrorText: brand.error,
    colorWarningText: brand.warningText,
    colorSuccessText: brand.successText,
    fontFamily: fontSans,
    fontFamilyCode: fontMono,
    borderRadius: 6,
  },
  components: {
    Layout: { siderBg: brand.navy, triggerBg: brand.navyDeep, headerBg: '#ffffff', bodyBg: brand.mist },
    Menu: {
      darkItemBg: brand.navy,
      darkSubMenuItemBg: brand.navyDeep,
      darkPopupBg: brand.navy,
      darkItemColor: brand.sidebarText,
      darkItemHoverColor: '#ffffff',
      darkItemSelectedBg: brand.primary,
      darkItemSelectedColor: '#ffffff',
      darkGroupTitleColor: brand.sidebarText,
    },
  },
};
