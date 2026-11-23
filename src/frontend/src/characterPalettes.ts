/**
 * Visual palettes and style options for the voxel cast.
 *
 * Each entry maps to a persona in `personaData.ts` (the Caldova Life Advice
 * practice team and its clients) or to someone the storybook adds (the
 * greeter at the front desk, family members and referral partners). Style
 * options (hair, glasses, beard, lower body shape, etc.) let the simple block
 * character read as a distinct individual.
 */

export type HairStyle =
  | "short"
  | "afro"
  | "ponytail"
  | "bun"
  | "bald"
  | "cap"
  | "hat"
  | "hood"
  | "crown"
  | "beanie"
  | "long";

export type FacialHair = "none" | "stubble" | "beard" | "moustache";

export type LowerBody = "pants" | "skirt" | "dress";

export interface VoxelCharacterPalette {
  /** Skin tone (face, hands). */
  skin: string;
  /** Hair / hat / hood color. */
  hair: string;
  /** Torso + arm color. */
  shirt: string;
  /** Pants / skirt / dress color. */
  pants: string;
  /** Shoe color. */
  shoes: string;
  /** Optional accent color used for tie / scarf / shirt logo / hat band. */
  accent?: string;
  /** Hair / headwear shape. Defaults to "short". */
  hairStyle?: HairStyle;
  /** Optional facial hair. */
  facial?: FacialHair;
  /** Render glasses on the face. */
  glasses?: boolean;
  /** Lower body shape. Defaults to "pants". */
  lowerBody?: LowerBody;
  /** Draw a small accent square logo on the chest. */
  shirtLogo?: boolean;
  /** Eye color. Defaults to dark brown. */
  eye?: string;
}

export const PALETTES: Record<string, VoxelCharacterPalette> = {
  // ----- Clients (one per scenario in personaData.ts) -----
  // Priya Natarajan (34): physiotherapist; smart blue top, black ponytail.
  clientFamily: {
    skin: "#c68e6a", hair: "#1a1410", shirt: "#3a8fd6", pants: "#2a3344",
    shoes: "#1a1a1a", accent: "#ffd166", hairStyle: "ponytail",
  },
  // Grace Williams (61): semi-retired principal; grey bun, cardigan, travel scarf.
  clientFraud: {
    skin: "#f3c79b", hair: "#c9c6c0", shirt: "#5a7fa8", pants: "#3a3a44",
    shoes: "#3a2a20", accent: "#e8a33d", hairStyle: "bun", lowerBody: "skirt",
  },
  // George Papadopoulos (52): master joiner; salt-and-pepper hair, moustache,
  // glasses and a mustard work shirt.
  clientBusiness: {
    skin: "#d9a37e", hair: "#6f655c", shirt: "#d9a03a", pants: "#2d3344",
    shoes: "#5a3a25", hairStyle: "short", glasses: true, facial: "moustache",
  },
  // Tom Bradley (41): self-employed electrician; beanie, beard, olive work shirt.
  clientIncome: {
    skin: "#f5d2b3", hair: "#7a5a3a", shirt: "#6f8f6a", pants: "#2a3344",
    shoes: "#1a1a1a", accent: "#e8e2d6", hairStyle: "beanie", facial: "beard",
  },
  // Robert Chen (44): teacher; quiet dark jacket and glasses.
  clientBereavement: {
    skin: "#e8c69a", hair: "#1f1a18", shirt: "#33353f", pants: "#1c1c22",
    shoes: "#1a1a1a", accent: "#22252e", hairStyle: "short", glasses: true,
  },

  // ----- Family and business partners of the clients -----
  // Arjun Mehta (36): Priya's partner, software engineer; teal tee and stubble.
  clientArjun: {
    skin: "#b27b56", hair: "#141010", shirt: "#2f8f8a", pants: "#3b4a63",
    shoes: "#f4f0e6", hairStyle: "short", facial: "stubble",
  },
  // Asha (2): Priya and Arjun's daughter; a pink dress and a little ponytail.
  clientAsha: {
    skin: "#c68e6a", hair: "#1a1410", shirt: "#f472b6", pants: "#f9a8d4",
    shoes: "#f8fafc", hairStyle: "ponytail", lowerBody: "dress",
  },
  // Helen Papadopoulos: George's wife; coral dress, fair hair.
  clientHelen: {
    skin: "#ebc19c", hair: "#d9c7a8", shirt: "#e07a5f", pants: "#e07a5f",
    shoes: "#7a4b2a", accent: "#f4d58d", hairStyle: "long", lowerBody: "dress",
  },
  // Harpreet Kaur (47): George's business partner, operations and finance
  // director; navy blazer, long black hair, gold accent.
  clientHarpreet: {
    skin: "#b07850", hair: "#121012", shirt: "#1f3a5f", pants: "#2a2f3a",
    shoes: "#1a1a1a", accent: "#c9a14a", hairStyle: "long", glasses: true, shirtLogo: true,
  },
  // Kylie Bradley (39): Tom's wife and support person; cardigan, fair ponytail.
  clientKylie: {
    skin: "#f1c7a1", hair: "#d8b26a", shirt: "#8b6fb3", pants: "#2f3a52",
    shoes: "#3a2a20", hairStyle: "ponytail",
  },

  // ----- Staff (the Caldova Life Advice practice team) -----
  // Staff shirts are matched to each person's accent colour (`color` in
  // personaData.ts), so each role reads with their desk and their agent's bay.
  // Olivia — Onboarding & Requests Officer (sky blue).
  onboardingOfficer: {
    skin: "#f3c79b", hair: "#3a2418", shirt: "#0ea5e9", pants: "#1e3a5f",
    shoes: "#1a1a1a", accent: "#ffd166", hairStyle: "ponytail", lowerBody: "dress",
  },
  // Penny — Paraplanner (purple): long dark hair, glasses, a gold pen accent.
  paraplanner: {
    skin: "#f5d2b3", hair: "#3b2416", shirt: "#a855f7", pants: "#2a2440",
    shoes: "#1a1a1a", accent: "#ffd166", hairStyle: "long", glasses: true,
    lowerBody: "skirt",
  },
  // Ivy — Insurance Research Analyst (amber): a researcher's bun and glasses,
  // amber cardigan, navy trousers.
  researchAnalyst: {
    skin: "#d9a37e", hair: "#2d1a10", shirt: "#f59e0b", pants: "#1f2d4a",
    shoes: "#1a1a1a", accent: "#7c4a12", hairStyle: "bun", glasses: true,
    shirtLogo: true,
  },
  // Felix — Financial Crime Investigator (red).
  finCrimeInvestigator: {
    skin: "#e8b48a", hair: "#1a1a1a", shirt: "#ef4444", pants: "#1c2230",
    shoes: "#1a1a1a", accent: "#1c2230", hairStyle: "short", facial: "stubble",
  },
  // Uma — Underwriting Coordinator (emerald): long black hair, white collar.
  underwritingCoord: {
    skin: "#a9714b", hair: "#0f0d0d", shirt: "#10b981", pants: "#33384a",
    shoes: "#1a1a1a", accent: "#f8fafc", hairStyle: "long",
  },
  // Ben — Benefit Claims Advocate (indigo): sandy hair, glasses, lanyard.
  claimsAdvocate: {
    skin: "#e8b48a", hair: "#8a5a2b", shirt: "#6366f1", pants: "#2d3344",
    shoes: "#3a2a20", accent: "#f8fafc", hairStyle: "short", glasses: true,
    facial: "beard",
  },
  // Cara — Client Communications Specialist (pink).
  commsSpecialist: {
    skin: "#8d5a3b", hair: "#1a1410", shirt: "#ec4899", pants: "#1f2d4a",
    shoes: "#1a1a1a", accent: "#c9a14a", hairStyle: "afro", lowerBody: "skirt",
  },
  // Theo — Practice Team Leader: navy suit, orange tie, beard.
  teamLeader: {
    skin: "#e8b48a", hair: "#5a4a35", shirt: "#26324f", pants: "#1c2438",
    shoes: "#1a1a1a", accent: "#f97316", hairStyle: "short", facial: "beard",
    shirtLogo: true,
  },

  // ----- The greeter at the front desk -----
  // Smart navy dress, long hair, gold scarf. Distinct from Olivia and Amelia so
  // the three read as different people when they are seen together.
  receptionGreeter: {
    skin: "#f0c8a4", hair: "#2a1a12", shirt: "#2a3a5c", pants: "#2a3a5c",
    shoes: "#1a1a1a", accent: "#ffd166", hairStyle: "long",
    lowerBody: "dress", shirtLogo: true,
  },
};
