# French Criminal Law Reference for AI Case Generation

**Purpose.** This file is a structured knowledge base for an AI system to generate realistic, legally grounded French criminal law practice scenarios ("cases") for users to play through — e.g., as a suspect, defense counsel, prosecutor, investigating judge, or juror. It covers general principles, offense definitions/elements/penalties, defenses, and the procedural pathway from offense to judgment, all drawn from the French *Code pénal* and *Code de procédure pénale*.

**Critical currency warning.** French criminal law is amended very frequently (multiple laws per year touch sentencing, aggravating circumstances, and procedure). The figures, article numbers, and aggravator lists below reflect a consolidated Code pénal / Code de procédure pénale text as researched, with amendment dates flagged inline where known. **Before generating a scenario that turns on an exact penalty ceiling, time limit, or monetary threshold, the generation system should treat this file as a structural/pedagogical scaffold, not a live legal-accuracy guarantee**, and should avoid presenting generated scenarios as current legal advice. Each offense block below includes a "last known amendment" note where available.

---

## 0. How to use this file to generate a case

A generated case should specify, at minimum:

1. **Jurisdiction & date of facts** — France; a specific date (affects which version of the law applies, per the non-retroactivity principle, Section 1.2).
2. **Role the user plays** — suspect/accused, defense lawyer, victim/partie civile, prosecutor (*procureur*), investigating judge (*juge d'instruction*), or juror (*cour d'assises*).
3. **Fact pattern** — built from one or more offense templates in Sections 3–6 below, including the material acts, the actors' mental states, and any circumstances that map to statutory aggravators or defenses.
4. **Procedural stage** — where in the pipeline (Section 2) the scenario begins: initial complaint, flagrance, garde à vue, charging decision, instruction, trial, or appeal.
5. **Difficulty levers** (see Section 7) — ambiguous intent, borderline attempt vs. preparation, competing qualifications (e.g., theft vs. breach of trust vs. fraud), contested defenses, procedural irregularities (e.g., unlawful garde à vue extension), or multiple co-offenders with different liability (author/complicit/corporate).
6. **Correct-answer key** — the applicable article(s), the elements the facts must satisfy, the available defenses and why they do/don't apply, the likely court and penalty range, and (if relevant) procedural issues.

A good scenario generator varies: (a) which offense(s) are implicated, (b) whether elements are clearly met or genuinely contestable, (c) whether a general defense (Section 1.6) applies, (d) the procedural posture, and (e) the players' incentives (prosecution vs. defense vs. civil party).

---

## 1. General Principles (droit pénal général) — Code pénal, Livre Ier

### 1.1 Classification of offenses — article 111-1

<claim cite-ids="a:page1#block3,a:page1#block4,a:page1#block5,a:page1#block6,a:page1#block7">Article 111-1, in Livre Ier, Titre Ier, Chapitre Ier, classifies criminal offenses by seriousness into three tiers: crimes, délits and contraventions.</claim> The tier is fixed by the statutorily prescribed penalty, not by how the facts are informally labeled:

| Tier | Penalty basis | Trial court | Example offenses |
|---|---|---|---|
| **Crime** | Criminal imprisonment/detention (*réclusion/détention criminelle*) under art. 131-1 | Cour d'assises / cour criminelle départementale | Murder, rape, armed robbery |
| **Délit** | Correctional penalties under art. 131-3 | Tribunal correctionnel | Theft, fraud, most assault, most sexual assault other than rape |
| **Contravention** | Fines/restrictions under arts. 131-12–131-13 | Tribunal de police | Minor regulatory breaches |

<claim cite-ids="a:page13#block3,a:page13#block4,a:page13#block5,a:page13#block6,a:page13#block7,a:page13#block8,a:page13#block9,a:page13#block10">Article 131-1 sets the principal penalties for crimes: life imprisonment/detention or fixed terms of thirty, twenty or fifteen years, with temporary criminal imprisonment or detention lasting at least ten years.</claim> <claim cite-ids="a:page22#block11,a:page22#block13,a:page22#block14,a:page22#block15,a:page22#block16,a:page22#block17,a:page22#block18,a:page22#block19">Article 131-13 sets five contravention classes with ordinary maximum fines of €38, €150, €450, €750 and €1,500, the top class rising to €3,000 for recidivism where the regulation so provides.</claim>

### 1.2 Legality principle and non-retroactivity

<claim cite-ids="a:page1#block11,a:page1#block12,a:page1#block13">Article 111-3 prohibits punishing anyone for a crime or délit whose elements are not statutorily defined, or for a contravention whose elements are not defined by regulation, and prohibits any penalty not provided by the applicable text.</claim> <claim cite-ids="a:page1#block14,a:page1#block15">Article 111-4 requires strict interpretation of criminal law ("La loi pénale est d'interprétation stricte"), barring courts from extending an offense by analogy.</claim>

<claim cite-ids="a:page2#block1,a:page2#block2,a:page2#block3,a:page2#block4,a:page2#block5">Article 112-1 provides: (i) only conduct criminal on the date of commission is punishable; (ii) only penalties legally applicable on that date may be imposed; and (iii) a new, less severe law applies retroactively to facts not yet finally adjudicated (*rétroactivité in mitius*).</claim> **Case-generation rule: always anchor a scenario to a specific date and apply the law as it stood then; use a more lenient intervening law if the "trial" occurs after a legislative change and before final judgment.**

### 1.3 Elements of an offense: material and moral

Every offense requires:
- **Material element (élément matériel):** the act, omission, result, causal link, or circumstance defined by the specific article.
- **Moral/mental element (élément moral):** <claim cite-ids="a:page8#block10,a:page8#block12">Article 121-3, first paragraph, establishes intent as the default mental element: there is no crime or délit without intent to commit it.</claim>

Statutory departures from pure intent:
- <claim cite-ids="a:page8#block13,a:page8#block14">Article 121-3 permits liability for deliberate endangerment and, where the specific offense provides, for negligence, imprudence or breach of a statutory/regulatory safety duty.</claim>
- <claim cite-ids="a:page8#block15,a:page9#block1">For indirect causal contribution, article 121-3 requires either a manifestly deliberate breach of a particular duty of care/safety, or a "faute caractérisée" exposing another to a particularly serious risk the person could not ignore.</claim>
- <claim cite-ids="a:page9#block2">For contraventions, force majeure excludes liability under article 121-3.</claim>

**Generator tip:** when building a negligence-based scenario (e.g., involuntary manslaughter, workplace accident), test whether the facts show (a) ordinary negligence sufficient for a *direct* cause, or (b) the heightened "manifestly deliberate breach" / "faute caractérisée" standard required for an *indirect* cause — this distinction is a rich source of difficulty.

### 1.4 Personal responsibility, attempt, complicity, corporate liability

- **Personal responsibility:** <claim cite-ids="a:page8#block1,a:page8#block2,a:page8#block3,a:page8#block4">Article 121-1 provides that no one is criminally responsible except for their own conduct ("Nul n'est responsable pénalement que de son propre fait").</claim>
- **Attempt (tentative):** <claim cite-ids="a:page9#block3,a:page9#block4,a:page9#block5,a:page9#block6">Article 121-4 treats a person who attempts a crime — or a délit where the law so provides — as an "auteur" (principal) of the offense.</claim> <claim cite-ids="a:page9#block7,a:page9#block8">Article 121-5 requires two cumulative elements: a commencement of execution (commencement d'exécution), and suspension or failure due to circumstances independent of the offender's will.</claim> A merely preparatory act does not qualify. **Voluntary abandonment** (désistement volontaire) — freely stopping before completion — defeats the attempt; abandonment caused by external resistance, discovery, or failure of a tool/accomplice does not, and the attempt remains punishable.
- **Complicity:** <claim cite-ids="a:page9#block9,a:page9#block11,a:page9#block12,a:page9#block13">Article 121-7 defines two forms of complicity: (1) knowingly facilitating preparation or commission of a crime/délit through aid or assistance, and (2) provoking the offense or giving instructions via gift, promise, threat, order, or abuse of authority/power.</claim> <claim cite-ids="a:page9#block9,a:page9#block10,a:page9#block11">Article 121-6 punishes the complice as an author of the offense.</claim> Complicity requires knowing participation (*sciemment*) and ordinarily presupposes a principal offense (attempted or completed).
- **Corporate criminal liability:** <claim cite-ids="a:page8#block5,a:page8#block6,a:page8#block7">Article 121-2 makes legal persons (other than the State) criminally liable for offenses committed for their account by their organs or representatives.</claim> <claim cite-ids="a:page8#block9">This does not exclude individual liability of the natural persons who were authors or complices of the same facts.</claim> For sentencing: <claim cite-ids="_a:page44#block1,_a:page44#block2,_a:page44#block3,_a:page44#block4,_a:page44#block5,_a:page44#block6,_a:page44#block7,_a:page44#block8,_a:page44#block9,_a:page44#block10,_a:page44#block11">the corporate fine is ordinarily five times the maximum fine applicable to a natural person.</claim> <claim cite-ids="_a:page44#block13,_a:page44#block15,_a:page44#block16,_a:page44#block17,_a:page44#block18,_a:page45#block1,_a:page45#block2,_a:page45#block5,_a:page45#block6">Additional corporate penalties under article 131-39 include dissolution (in specified circumstances), professional-activity prohibition, judicial supervision, closure, public-procurement exclusion, confiscation, and publication of the conviction.</claim>

**Generator tip for corporate scenarios:** test separately (1) existence of an offense, (2) action by an organ/representative, (3) conduct for the company's account, and (4) possible concurrent liability of the individual decision-maker.

### 1.5 Sentencing principles

<claim cite-ids="a:page12#block8,a:page12#block9,a:page12#block11,a:page12#block12,a:page12#block13">Article 130-1 identifies both punitive and rehabilitative sentencing functions: sanctioning the offender and promoting amendment, integration or reintegration, while protecting society and respecting victims' interests.</claim> The individualization principle (art. 132-1) requires the court to set the nature, amount and regime of the penalty based on the circumstances and the offender's personal situation.

Main penalty types for individuals:
- **Crimes:** life or fixed-term (30/20/15-year) criminal imprisonment/detention (art. 131-1).
- **Délits** (art. 131-3): imprisonment (possibly suspended — *sursis*/*sursis probatoire*), electronic home detention, <claim cite-ids="a:page19#block4,a:page19#block5,a:page19#block6,a:page19#block7,a:page19#block8,a:page19#block9">community service (*travail d'intérêt général*, 20–400 hours, art. 131-8, subject to defendant consent when present)</claim>, fines, day-fines, "stages," rights restrictions, sanction-reparation.
- **Contraventions** (art. 131-12): fines and rights restrictions.
- **Complementary penalties** (arts. 131-10–131-11): professional bans, disqualifications, confiscation, closure of establishments, publication of the decision.

**Recidivism** (arts. 132-8–132-16) can increase the applicable maximum, commonly by doubling it, depending on the identity/timing of the prior and new offenses — check the specific article pairing before hard-coding a multiplier.

### 1.6 Grounds excluding or reducing criminal responsibility — arts. 122-1 to 122-9

These are the primary **defenses** available to a "suspect/defense" player role. Code pénal, Livre Ier, Titre II, Chapitre II:

| Ground | Article | Conditions |
|---|---|---|
| **Mental disorder** | 122-1 | <claim cite-ids="a:page9#block14,a:page9#block15,a:page9#block17">Complete irresponsibility where a psychological/neuropsychological disorder abolished discernment or control of acts at the time of the facts.</claim> <claim cite-ids="a:page9#block18,a:page10#block1">Mere alteration (not abolition) of discernment preserves liability but must be considered in sentencing; custodial sentence ordinarily reduced by one third (life → 30 years), subject to a reasoned exception.</claim> Note: <claim cite-ids="a:page10#block2,a:page10#block3,a:page10#block4,a:page10#block5,a:page10#block6,a:page10#block7">arts. 122-1-1 and 122-1-2 (introduced 24 January 2022) restrict this benefit where the mental state resulted from voluntary consumption of psychoactive substances in specified circumstances.</claim> |
| **Duress/constraint** | 122-2 | <claim cite-ids="a:page10#block8,a:page10#block9">No responsibility where the person acted under irresistible force or constraint.</claim> Ordinary pressure or financial difficulty does not qualify. |
| **Mistake of law** | 122-3 | <claim cite-ids="a:page10#block10,a:page10#block11">No responsibility if an unavoidable mistake about the law led the person to believe the act was lawful.</claim> Narrow; ignorance of the law alone is insufficient; burden on defendant. |
| **Order of law / command of authority** | 122-4 | <claim cite-ids="a:page10#block12,a:page11#block1">No responsibility for an act prescribed/authorized by law or regulation, or commanded by a legitimate authority — unless the command was manifestly unlawful.</claim> |
| **Legitimate defense (self-defense)** | 122-5, 122-6 | <claim cite-ids="a:page11#block2,a:page11#block3">Requires an unjustified attack, contemporaneous defensive action, necessity, and proportionality of means to the seriousness of the attack.</claim> <claim cite-ids="a:page11#block4">For defense of property: strictly necessary to interrupt a crime/délit against property, must not be intentional homicide, and proportionate.</claim> <claim cite-ids="a:page11#block5,a:page11#block6,a:page11#block7,a:page11#block8">Article 122-6 creates statutory presumptions of legitimate defense for (1) repelling nighttime break-in to an inhabited dwelling, and (2) defending against violent theft or pillage.</claim> |
| **State of necessity** | 122-7 | <claim cite-ids="a:page11#block9,a:page11#block10">No responsibility where, faced with an actual or imminent danger to a person or property, the defendant commits a necessary safeguarding act, proportionate to the threat.</claim> Unlike self-defense, the danger need not stem from a human attack. |
| **Minority** | 122-8 | <claim cite-ids="a:page11#block11,a:page11#block13">Minors capable of discernment are criminally responsible but benefit from age-based mitigation under the Code de la justice pénale des mineurs (CJPM).</claim> |
| **Victim consent** | (no general article) | No general statutory defense. Effect is offense-specific: irrelevant for offenses protecting non-disposable interests (life, sexual autonomy, public order); potentially relevant where the protected interest is disposable by the victim. Treat case-by-case. |

**Generator tip:** a well-built defense scenario tests the *boundary* conditions — e.g., disproportionate self-defense (excessive retaliation after the attack ended), state of necessity where a cheaper/legal alternative existed, or mistake of law based on bad legal advice (usually insufficient).

---

## 2. Criminal Procedure Pipeline — Code de procédure pénale

Use this section to place a scenario at a specific procedural stage and to script realistic actions by police, prosecutor, judges, and counsel.

### 2.1 Foundational principles

<claim cite-ids="Va:page1#block3,Va:page1#block5,Va:page1#block6,Va:page1#block9,Va:page1#block10,Va:page1#block12,Va:page1#block13">The preliminary article (Article préliminaire) requires a fair, adversarial procedure, separation of prosecutorial and adjudicative authority, presumption of innocence, access to counsel, proportionate coercive measures, and judgment within a reasonable time.</claim> <claim cite-ids="Va:page2#block4,Va:page112#block2,Va:page193#block7,Va:page405#block10">The right to silence applies at investigation, instruction and trial stages, and must be notified to the person.</claim>

### 2.2 Investigation

**Police judiciaire & prosecutor.** <claim cite-ids="Va:page29#block1,Va:page29#block6,Va:page29#block8">The police judiciaire operates under the prosecutor's direction (and the procureur général's supervision), establishing offenses, collecting evidence, and identifying perpetrators before an investigation is opened (CPP arts. 12–15).</claim> <claim cite-ids="Va:page59#block10,Va:page59#block11,Va:page60#block4,Va:page62#block4,Va:page62#block5">The prosecutor directs investigators, controls legality/proportionality of investigative acts, and receives complaints (CPP arts. 39-3, 40, 41).</claim>

**Two investigation tracks:**
- **Enquête de flagrance** (CPP art. 53 and following): <claim cite-ids="Va:page87#block6,Va:page87#block8">applies where an offense is being committed or has just been committed, including immediate pursuit by public outcry or discovery of incriminating objects/traces.</claim> <claim cite-ids="Va:page87#block9,Va:page87#block10">May run continuously for 8 days, extendable by the prosecutor for a further 8 days for a crime or offense punishable by at least 5 years.</claim>
- **Enquête préliminaire** (CPP arts. 75–78): the default track absent flagrance, conducted under prosecutorial direction. <claim cite-ids="Va:page131#block8,Va:page131#block10">Article 77 applies the garde à vue/interview rules of arts. 61-1, 61-2, 62-2–64-1.</claim> <claim cite-ids="Va:page134#block8,Va:page134#block10,Va:page135#block1,Va:page136#block2">Article 77-2 (as amended by Law No. 2023-1059 of 20 November 2023) allows file access and observations by the suspect/victim in specified circumstances.</claim>

**Garde à vue (police custody).** <claim cite-ids="Va:page109#block4,Va:page109#block6,Va:page109#block7,Va:page109#block8,Va:page109#block9,Va:page109#block10,Va:page109#block11,Va:page109#block12,Va:page109#block13">Requires plausible grounds to suspect a crime or imprisonable délit, and must be the sole means of achieving a listed objective (preserving evidence, preventing collusion/pressure on witnesses, ensuring appearance, preventing continuation of the offense) — CPP arts. 62-2, 63.</claim> <claim cite-ids="Va:page110#block5,Va:page110#block7,Va:page110#block8,Va:page110#block9,Va:page110#block10,Va:page111#block1">Ordinary duration: 24 hours, extendable once by up to 24 more hours on written, reasoned prosecutorial authorization for an offense punishable by at least 1 year.</claim> Longer special regimes exist for organized crime/terrorism (CPP arts. 706-88 et seq.) — flag these as exceptional, not default.

Rights during garde à vue (<claim cite-ids="Va:page111#block4,Va:page111#block6,Va:page111#block7,Va:page111#block8,Va:page111#block10,Va:page111#block11,Va:page111#block12,Va:page112#block1,Va:page112#block2">CPP art. 63-1</claim>): prompt notification of the measure/duration/offense/reasons, right to notify a relative/employer/consular authority, medical exam, interpreter, lawyer, access to specified documents, and the right to remain silent. <claim cite-ids="Va:page115#block6,Va:page115#block8,Va:page115#block10,Va:page116#block4,Va:page116#block6,Va:page116#block7,Va:page116#block9,Va:page116#block11,Va:page117#block2,Va:page117#block4">The lawyer may be requested from the outset, consult confidentially (ordinarily up to 30 minutes), access specified documents, and attend interviews/confrontations (CPP arts. 63-3-1, 63-4, 63-4-1, 63-4-2).</claim> *(Last known amendment to garde à vue rules: Law No. 2024-364 of 22 April 2024, effective 1 July 2024.)*

### 2.3 Prosecutorial decisions

<claim cite-ids="Va:page60#block2,Va:page60#block4,Va:page60#block6,Va:page60#block8,Va:page60#block9,Va:page60#block10,Va:page60#block11">Article 40-1 gives the prosecutor three options once an offense is established with no legal bar: prosecute; use an alternative under arts. 41-1, 41-1-2 or 41-2; or classify without further action (classement sans suite).</claim> <claim cite-ids="Va:page60#block12,Va:page60#block14,Va:page60#block15,Va:page61#block1,Va:page61#block2">A classement sans suite must be notified to complainants/victims with reasons, and is challengeable before the procureur général (CPP arts. 40-2, 40-3).</claim>

**Alternatives to prosecution:**
- <claim cite-ids="Va:page63#block5,Va:page63#block6,Va:page63#block7,Va:page64#block1,Va:page64#block2,Va:page64#block3,Va:page64#block4,Va:page64#block5,Va:page65#block3,Va:page65#block4,Va:page66#block1">Article 41-1: restorative/rehabilitative measures — avertissement pénal probatoire (replacing the former rappel à la loi per Law No. 2023-140 of 28 February 2023), referral to care, compensation, mediation, no-contact orders, civic contribution.</claim>
- <claim cite-ids="Va:page70#block4,Va:page70#block5,Va:page70#block6,Va:page70#block7,Va:page71#block3,Va:page71#block4,Va:page71#block6,Va:page71#block7,Va:page72#block2,Va:page72#block4,Va:page72#block7,Va:page73#block1">Composition pénale (art. 41-2): for a natural person who acknowledges an eligible délit (fine or up to 5 years' imprisonment); measures include a fine, surrender of proceeds/licence, unpaid work, no-contact obligations, etc.</claim> <claim cite-ids="Va:page73#block4,Va:page73#block5,Va:page74#block1,Va:page74#block3,Va:page74#block5">Requires informed acceptance, may need judicial validation, and extinguishes the public action on completion; victim retains civil remedies.</claim>
- **CRPC** (comparution sur reconnaissance préalable de culpabilité, CPP arts. 495-7–495-16): a negotiated-plea mechanism — the prosecutor proposes a sentence, defendant (assisted by counsel) accepts, and a judge homologates. Distinct from composition pénale: CRPC results in a conviction with the effects of a judgment.

**Opening a judicial investigation (information judiciaire).** <claim cite-ids="Va:page153#block8,Va:page153#block11,Va:page154#block1">Mandatory for crimes, generally optional for délits, possible for contraventions in specified cases (CPP art. 79).</claim> <claim cite-ids="Va:page86#block1,Va:page154#block2,Va:page154#block4,Va:page166#block7,Va:page167#block3">The investigating judge is seized by the prosecutor's réquisitoire introductif or a victim's complaint with constitution de partie civile.</claim>

### 2.4 Pre-trial judicial supervision

- **Mise en examen** (formal charging): <claim cite-ids="Va:page155#block8,Va:page156#block1,Va:page156#block2,Va:page156#block3,Va:page156#block4">requires "indices graves ou concordants" making participation plausible; the judge must first enable observations with counsel and must prefer témoin assisté status if sufficient (art. 80-1).</claim>
- **Témoin assisté:** <claim cite-ids="Va:page187#block11,Va:page187#block14,Va:page187#block16,Va:page188#block1,Va:page188#block2">intermediate status for a person named or plausibly implicated but not formally charged (arts. 113-1, 113-2)</claim>; <claim cite-ids="Va:page188#block4,Va:page188#block6,Va:page188#block9,Va:page189#block1">has counsel/file-access rights but cannot be subject to judicial control, electronic detention, or pre-trial detention.</claim>
- **Contrôle judiciaire / assignation à résidence:** supervisory obligations (CPP art. 138) or electronic home detention (arts. 142-5–142-13) as alternatives to detention — <claim cite-ids="Va:page220#block8,Va:page220#block10,Va:page220#block11,Va:page221#block2,Va:page221#block4,Va:page224#block12,Va:page224#block14">the Code's hierarchy favors liberty, then supervision, then detention (art. 137).</claim>
- **Détention provisoire (pre-trial detention):** <claim cite-ids="Va:page225#block10,Va:page225#block11,Va:page225#block13,Va:page225#block14,Va:page225#block15,Va:page225#block16">available principally where facing a criminal sentence or a correctional sentence of at least 3 years (art. 143-1).</claim> <claim cite-ids="Va:page226#block1,Va:page226#block2,Va:page226#block3,Va:page226#block4,Va:page226#block5,Va:page226#block6,Va:page226#block7,Va:page226#block8,Va:page226#block9,Va:page226#block12,Va:page226#block13">Must be the unique means of achieving a listed objective (preserving evidence, preventing collusion, protecting the person, ensuring availability, preventing repetition) and proportionate (arts. 144, 144-1).</claim> <claim cite-ids="Va:page227#block3,Va:page227#block5,Va:page227#block6,Va:page227#block8,Va:page227#block9,Va:page228#block2,Va:page228#block3,Va:page228#block4">Decided by the juge des libertés et de la détention (JLD) after referral and a contradictory hearing (art. 145).</claim>

### 2.5 Courts and jurisdiction

| Offense tier | Trial court | Composition | Key CPP articles |
|---|---|---|---|
| Contravention | Tribunal de police | Single judge | 521–549 |
| Délit | Tribunal correctionnel | <claim cite-ids="Va:page398#block7,Va:page398#block9,Va:page403#block5">Ordinarily a president + two judges, subject to statutory single-judge exceptions</claim> | 381–469 |
| Crime | Cour d'assises | <claim cite-ids="Va:page324#block5,Va:page324#block7,Va:page326#block7,Va:page326#block9,Va:page326#block11">Professional judges + jury; jurisdiction over persons referred by mise en accusation</claim> | 231–380-15 |
| Crime (eligible subset) | Cour criminelle départementale | Professional judges only (no jury); for certain crimes punishable by 15–20 years, committed by adults, absent disqualifying recidivism | 380-16–380-22 |

**Appeals:** tribunal de police → cour d'appel (arts. 546–549); tribunal correctionnel → chambre des appels correctionnels (arts. 496–520); cour d'assises → assize appeal (arts. 380-1–380-15); investigation-stage orders/detention/nullities → chambre de l'instruction (arts. 185–207); final legal review → Cour de cassation (arts. 567–621, reviews law/reasoning, not facts).

### 2.6 Trial roles

- **Ministère public (prosecutor):** <claim cite-ids="Va:page58#block8,Va:page418#block14,Va:page418#block16">represents society, conducts the prosecution and presents requisitions (CPP arts. 31, 32, 39, 458).</claim>
- **Defense:** <claim cite-ids="Va:page409#block4,Va:page419#block3,Va:page419#block9,Va:page419#block10">challenges evidence/legal basis, presents arguments; the defendant/counsel speaks last (arts. 417, 459, 460).</claim>
- **Partie civile (victim):** <claim cite-ids="Va:page3#block1,Va:page3#block2,Va:page16#block2,Va:page409#block8,Va:page409#block10,Va:page409#block12,Va:page411#block3,Va:page411#block9">may join proceedings and claim damages for directly caused material, bodily and moral harm (arts. 2–3, 418–424).</claim>
- **Investigating judge:** <claim cite-ids="Va:page159#block5,Va:page159#block6">must investigate both à charge and à décharge (art. 81).</claim>
- **Jury:** <claim cite-ids="Va:page326#block9,Va:page329#block4,Va:page329#block6,Va:page329#block8">citizen jury participates in the cour d'assises (arts. 240, 254–255).</claim>

**Proof standard:** <claim cite-ids="Va:page412#block5,Va:page412#block6,Va:page412#block7,Va:page412#block8,Va:page412#block11">free proof and the judge's intime conviction, but evidence must be produced and contradictorily discussed at the hearing; an admission is only one item of evidence, freely assessed (CPP arts. 427–428).</claim>

**Outcomes:** acquittement (crime), relaxe (délit), non-lieu (end of instruction, insufficient evidence, arts. 176–177), or conviction + sentence.

### 2.7 Prescription (statute of limitations) of the public action

| Tier | Period | Article |
|---|---|---|
| Crime | <claim cite-ids="Va:page18#block1,Va:page18#block3">20 years from commission</claim> | CPP art. 7 |
| Délit | <claim cite-ids="Va:page18#block7,Va:page18#block9">6 years from commission</claim> | CPP art. 8 |
| Contravention | <claim cite-ids="Va:page19#block3,Va:page19#block5">1 year from commission</claim> | CPP art. 9 |

<claim cite-ids="Va:page18#block4,Va:page18#block5,Va:page18#block6,Va:page18#block10,Va:page18#block11,Va:page18#block12">Extended 30-year periods apply to specified terrorism/organized-crime/other serious crimes; crimes against humanity are imprescriptible; certain sexual offenses against minors run from the victim's majority.</claim> <claim cite-ids="Va:page19#block6,Va:page19#block9,Va:page19#block10,Va:page19#block11">For occult/concealed offenses, the period runs from discoverability, capped at 12 years (délit) / 30 years (crime) from commission (art. 9-1).</claim> <claim cite-ids="Va:page20#block1,Va:page20#block2,Va:page20#block3,Va:page20#block4,Va:page20#block5,Va:page20#block6">Prosecutorial, investigative and judicial acts interrupt the period, restarting an equal new period (art. 9-2).</claim>

---

## 3. Offenses Against Persons — Code pénal, Livre II

*Currency note: <claim cite-ids="pa:page137#block5,pa:page137#block12,pa:page138#block15">sexual-offense provisions in the consulted text are marked amended 6 November 2025</claim>, <claim cite-ids="pa:page139#block12,pa:page142#block7,pa:page144#block4">with further amendment 18 August 2026</claim>; <claim cite-ids="pa:page101#block8,pa:page107#block9,pa:page111#block4">road-homicide provisions were revised in 2025</claim>. Verify current text before precise use.*

### 3.1 Homicide

| Offense | Article | Material element | Moral element | Penalty |
|---|---|---|---|---|
| Murder (meurtre) | 221-1 | <claim cite-ids="pa:page97#block6,pa:page97#block7">Voluntarily causing the death of another</claim> | Intent to kill (animus necandi) | 30 years' réclusion criminelle |
| Assassination | 221-3 | Murder + premeditation or ambush | <claim cite-ids="pa:page97#block12,pa:page97#block14">Same as murder, with premeditation/guet-apens</claim> | Life imprisonment |
| Murder connected to another felony | 221-2 | <claim cite-ids="pa:page97#block8,pa:page97#block10">Murder preceding/accompanying/following another crime, or to facilitate/conceal a felony</claim> | Intent to kill | Life imprisonment |
| Aggravated murder (various victims/circumstances) | 221-4 | <claim cite-ids="pa:page98#block2,pa:page98#block9,pa:page99#block1,pa:page99#block4">Victim under 15; ascendant; vulnerable/subjected person; protected officials and their families; witness/victim/civil party; organized group; spouse/cohabitant/partner; refusal-of-marriage motive; offender manifestly intoxicated</claim> | Intent to kill | Life imprisonment |
| Poisoning (empoisonnement) | 221-5 | <claim cite-ids="pa:page99#block6,pa:page99#block10">Administering substances capable of causing death; death need not occur</claim> | Intentional attack on life, knowledge of lethal character | 30 years, life if 221-2/3/4 circumstances present |
| Involuntary manslaughter | 221-6 | <claim cite-ids="pa:page101#block3,pa:page101#block7">Causing death via maladresse, imprudence, inattention, negligence or breach of a duty of care</claim> | No intent to kill; fault per art. 121-3 | 3 years/€45,000; 5 years/€75,000 for manifestly deliberate breach of a particular safety duty |

<claim cite-ids="pa:page100#block5,pa:page100#block9">A cooperation provision (art. 221-5-3) exempts from punishment an attempted murderer/poisoner who, after warning authorities, prevents death; otherwise a warning identifying accomplices or preventing repetition can reduce the sentence by two-thirds (life → 15 years).</claim>

**Generator tip:** the murder/assassination/aggravated-violence boundary is a classic difficulty lever — vary whether facts show premeditation (planning, acquisition of a weapon in advance, reconnaissance) vs. a sudden/unplanned act, and whether death was intended vs. merely risked.

### 3.2 Voluntary violence (assault)

Tiered by resulting harm (ITT = *incapacité totale de travail*, a legal-medical measure, not necessarily employment incapacity):

| Result | Base article | Base penalty |
|---|---|---|
| Death without intent to kill | 222-7 | <claim cite-ids="pa:page116#block11,pa:page116#block13">15 years' réclusion criminelle</claim> |
| Permanent mutilation/disability | 222-9 | <claim cite-ids="pa:page119#block1,pa:page119#block3">10 years, €150,000</claim> |
| ITT > 8 days | 222-11 | <claim cite-ids="pa:page121#block4,pa:page121#block6">3 years, €45,000</claim> |
| ITT ≤ 8 days or none | 222-13 | <claim cite-ids="pa:page123#block14,pa:page124#block1">3 years, €45,000 — only if an aggravating circumstance is present</claim> |

<claim cite-ids="pa:page119#block7,pa:page120#block13,pa:page121#block9,pa:page122#block11,pa:page123#block8,pa:page125#block1,pa:page126#block3">Recurring aggravating circumstances across these tiers: victim under 15; apparent/known vulnerability; known psychological/physical subjection; ascendant victim; protected officials (judicial, police, military, prison, education, transport, healthcare); spouse/cohabitant/partner; witness/victim/civil-party status; discriminatory motive (ethnicity, religion, sex, orientation, gender identity); prostitution-related; forced-marriage motive; multiple perpetrators; premeditation/ambush; weapon; specified locations (schools, transport, healthcare); adult using a minor as accomplice; manifest intoxication; face concealment.</claim> Penalty escalation examples: <claim cite-ids="pa:page117#block1,pa:page118#block12,pa:page118#block15">death-without-intent rises from 15 to 20 years for one aggravator, 30 years for child-under-15/ascendant or minor-witnessed domestic violence</claim>; <claim cite-ids="pa:page121#block7,pa:page123#block9,pa:page123#block12">ITT-over-8-days violence rises from 3 to 5/7/10 years depending on the number of aggravators.</claim>

<claim cite-ids="pa:page126#block8,pa:page127#block3">Habitual violence against a child under 15, a vulnerable person, or by a domestic partner (art. 222-14) carries result-based penalties from 5 years (no/low ITT) up to 30 years (death).</claim> <claim cite-ids="pa:page128#block4,pa:page128#block6">The violence provisions apply to psychological violence as well as physical (art. 222-14-3).</claim>

**Defenses/qualification issues:** absence of a voluntary violent act, causation disputes, contesting the medical ITT/permanent-injury finding, contesting knowledge of vulnerability/official status, contesting the domestic relationship, and the general defenses (self-defense, necessity) from Section 1.6.

### 3.3 Sexual offenses

**Consent standard (art. 222-22, 222-22-1):** <claim cite-ids="pa:page137#block4,pa:page137#block9">sexual assault is any non-consensual sexual act; consent must be free, informed, specific, prior and revocable, assessed in context, and cannot be inferred from silence or lack of reaction alone; non-consent exists where the act is committed through violence, coercion, threat or surprise.</claim> <claim cite-ids="pa:page137#block11,pa:page137#block14,pa:page138#block1">Coercion may be physical or moral; for a minor, moral coercion/surprise may arise from age difference and authority; for a child under 15, abuse of vulnerability suffices where the child lacks the discernment necessary for the act.</claim>

| Offense | Article | Key elements | Penalty |
|---|---|---|---|
| Rape | 222-23 | <claim cite-ids="pa:page138#block14,pa:page138#block17">Penetration or bucco-genital/bucco-anal act via violence, coercion, threat or surprise</claim> | 15 years |
| Child rape (age-gap) | 222-23-1, 222-23-3 | <claim cite-ids="pa:page139#block1,pa:page139#block4,pa:page139#block8,pa:page139#block10">Adult-to-child-under-15 penetration/bucco-genital act, age gap ≥5 years (exception if remuneration involved)</claim> | 20 years |
| Incestuous rape | 222-22-3, 222-23-2, 222-23-3 | <claim cite-ids="pa:page138#block7,pa:page138#block12,pa:page139#block5,pa:page139#block10">Adult ascendant/authority figure, minor victim</claim> | 20 years |
| Aggravated rape | 222-24 | <claim cite-ids="pa:page139#block11,pa:page140#block14">Permanent injury; victim under 15/vulnerable; ascendant/authority perpetrator; multiple perpetrators; weapon; domestic partner; drugging; minor witness; public transport</claim> | 20 years |
| Rape causing death / with torture | 222-25, 222-26 | <claim cite-ids="pa:page140#block15,pa:page141#block5">Death results / preceded, accompanied or followed by torture or barbarity</claim> | 30 years / life |
| Sexual assault (other than rape) | 222-27 | <claim cite-ids="pa:page142#block2,pa:page142#block5">Non-consensual sexual act not amounting to rape</claim> | 5 years, €75,000 |
| Aggravated sexual assault | 222-28, 222-29, 222-29-1 to -3 | <claim cite-ids="pa:page142#block6,pa:page143#block4,pa:page143#block5,pa:page143#block10,pa:page143#block11,pa:page143#block14,pa:page144#block2">Injury/ITT>8 days, authority, multiple perpetrators, weapon, vulnerability, child under 15, incest</claim> | 7–10 years |
| Drug-facilitated sexual offense | 222-30-1 | <claim cite-ids="pa:page144#block15,pa:page145#block1">Administering a substance without knowledge to impair discernment for purposes of rape/assault</claim> | 5–7 years |
| Sexual exhibition | 222-32 | <claim cite-ids="pa:page145#block9,pa:page145#block14">Imposing sexual exhibition/explicit act on public view</claim> | 1 year (2 years if victim under 15) |
| Sexual harassment | 222-33 | <claim cite-ids="pa:page145#block15,pa:page146#block5">Repeated sexual/sexist remarks/conduct violating dignity or creating a hostile environment; group or successive repetition; or one-off serious pressure for a sexual act</claim> | 2 years, €30,000 (3 years/€45,000 aggravated) |

### 3.4 Harassment offenses

| Type | Article | Key elements | Penalty |
|---|---|---|---|
| Workplace moral harassment | 222-33-2 | <claim cite-ids="pa:page148#block4,pa:page148#block7">Repeated conduct degrading working conditions, harming dignity/health/career</claim> | 2 years, €30,000 |
| Domestic harassment | 222-33-2-1 | <claim cite-ids="pa:page148#block8,pa:page148#block12">Repeated conduct against a spouse/partner/cohabitant (current or former) degrading living conditions and health</claim> | 3–5 years (10 years if suicide/attempt results) |
| General harassment (incl. cyberstalking) | 222-33-2-2 | <claim cite-ids="pa:page149#block2,pa:page149#block5">Repeated conduct degrading living conditions and health; collective/successive repetition counts</claim> | 1–3 years depending on ITT, vulnerability, online means, elected-official victim |
| School harassment | 222-33-2-3 | <claim cite-ids="pa:page149#block14,pa:page150#block5">Moral harassment of a student by someone in the same institution</claim> | 3–10 years depending on result |

### 3.5 Endangerment and failure to assist

| Offense | Article | Elements | Penalty |
|---|---|---|---|
| Deliberate endangerment | 223-1 | <claim cite-ids="pa:page165#block3,pa:page165#block7">Direct exposure to immediate risk of death/mutilation via manifestly deliberate breach of a particular safety duty (result-independent)</claim> | 1 year, €15,000 |
| Abandonment of a vulnerable person | 223-3, 223-4 | <claim cite-ids="pa:page167#block6,pa:page167#block12">Abandoning a person unable to protect themselves; aggravated to 15 years (permanent injury) or 20 years (death)</claim> | 5 years base |
| Failure to assist | 223-6 | <claim cite-ids="pa:page168#block1,pa:page168#block4">Failing to prevent a bodily-integrity offense or assist a person in danger when it could be done safely; 7 years if victim is a child under 15</claim> | 5–7 years, €75,000–€100,000 |

**Key defense/qualification issue for art. 223-6:** absence of risk to the rescuer/third parties is a statutory precondition — the scenario should make clear whether intervention was safely possible.

### 3.6 Kidnapping / unlawful confinement

<claim cite-ids="pa:page177#block1,pa:page177#block5">Article 224-1: arresting, abducting, detaining or confining a person without lawful authority — 20 years' réclusion criminelle</claim>, <claim cite-ids="pa:page177#block3,pa:page177#block5">reduced to 5 years/€75,000 for voluntary release before the 7th day (except specified aggravated cases).</claim> Aggravations run from <claim cite-ids="pa:page177#block6,pa:page177#block9,pa:page178#block1,pa:page178#block6,pa:page178#block13,pa:page179#block1">30 years (permanent injury/multiple victims/hostage-taking) to life imprisonment (torture, death, or victim under 15 combined with a 30-year circumstance).</claim>

---

## 4. Property and Financial Offenses — Code pénal, Livre III

*Currency note: multiple provisions amended 2022–2026; see per-offense notes.*

### 4.1 Cross-offense comparison (use to build "which offense applies?" puzzles)

<claim cite-ids="Fa:page238#block12,Fa:page250#block12,Fa:page255#block8,Fa:page245#block13,Fa:page260#block11..Fa:page260#block12,Fa:page280#block11..Fa:page280#block12,Fa:page265#block16,7cb1a37e-6763-4ba2-838f-e4d757b51f2c">

| Offense | Initial possession/transfer | Means | Core mental element |
|---|---|---|---|
| Theft | No lawful transfer; taking without consent | Fraudulent taking | Intent to take another's thing |
| Fraud | Victim voluntarily transfers | False identity/quality, abuse of quality, fraudulent manoeuvres | Intentional deception causing transfer |
| Breach of trust | Lawful prior delivery for return/accounting/specified use | Later diversion | Knowing violation of entrusted purpose |
| Extortion | Victim delivers/signs under coercion | Violence, threat, constraint | Intent to obtain through coercion |
| Recel (receiving) | Possession/benefit follows a predicate offense | No need to have committed the original offense | Knowledge of criminal origin |
| Money laundering | Criminal proceeds placed/concealed/converted | Concealment/legitimization of origin | Knowledge + intentional assistance |
| Property damage | No transfer required | Physical destruction/deterioration | Intentional damage-producing conduct |
| Corporate asset misuse (abus de biens sociaux) | Corporate property/credit/power available via office | Abuse of corporate position | Bad faith, contrary to company interest, personal/related-party purpose |
</claim>

### 4.2 Theft (vol)

<claim cite-ids="Fa:page238#block7,Fa:page238#block8,Fa:page238#block11,Fa:page238#block12">Article 311-1 defines theft as "la soustraction frauduleuse de la chose d'autrui" — the fraudulent taking of another's property.</claim> <claim cite-ids="Fa:page239#block1">Basic penalty: 3 years, €45,000 (art. 311-3).</claim> <claim cite-ids="Fa:page238#block13,Fa:page238#block14">Fraudulent abstraction of energy (e.g., electricity) is assimilated to theft (art. 311-2).</claim>

**Aggravations (cumulative circumstances raise the penalty):**
- <claim cite-ids="Fa:page239#block5,Fa:page239#block7..Fa:page239#block14,Fa:page240#block1..Fa:page240#block4">Art. 311-4 (5 years/€75,000): multiple perpetrators (not organized group), abuse of public authority, violence without total incapacity, theft of medical equipment/in healthcare settings, theft in a dwelling or from public-transport premises, accompanying damage, agricultural premises, face concealment, school premises, illegal animal trading.</claim> <claim cite-ids="Fa:page240#block5">Two circumstances → 7 years/€100,000; three → 10 years/€150,000.</claim>
- <claim cite-ids="Fa:page241#block3..Fa:page241#block9">Art. 311-5 (7 years/€100,000, 10 years/€150,000 combined): violence causing ITT ≤8 days, exploitation of apparent/known vulnerability, breaking/entering/climbing into a dwelling.</claim>
- <claim cite-ids="Fa:page241#block10..Fa:page242#block2">Violence causing ITT >8 days: 10 years/€150,000 (art. 311-6); permanent mutilation: 15 years (art. 311-7).</claim>
- <claim cite-ids="Fa:page242#block3..Fa:page242#block6">Weapon use/threat or carrying a prohibited weapon: 20 years (art. 311-8).</claim>
- <claim cite-ids="Fa:page242#block7..Fa:page243#block5">Organized-group theft: 15 years base, 20 years with violence, 30 years with weapon, life with death/torture (art. 311-9).</claim>

**Family immunity:** <claim cite-ids="Fa:page243#block9..Fa:page243#block16">Article 311-12 generally bars prosecution for theft between ascendant/descendant/spouse, subject to exceptions (ID/residence/payment documents; protected-person representative).</claim> *(Last known amendment: art. 311-4, Law No. 2026-796 of 18 August 2026.)*

### 4.3 Fraud (escroquerie)

<claim cite-ids="Fa:page250#block8..Fa:page250#block13">Article 313-1: deceiving a person by false name, false capacity, abuse of a true capacity, or fraudulent manoeuvres, thereby inducing surrender of funds/property, a service, or an obligation-creating act, to the detriment of the victim or a third party. Basic penalty: 5 years, €375,000.</claim> Prejudice to a third party (not necessarily the deceived person) suffices. <claim cite-ids="Fc:html41..Fc:html44">The Cour de cassation recognizes "escroquerie au jugement" — obtaining a prejudicial court decision by deceiving the court via a false document (24 October 2018 decision).</claim>

**Distinguishing feature vs. theft:** the victim *voluntarily* transfers due to deception, rather than having property taken without consent.

**Aggravation:** <claim cite-ids="Fa:page251#block1..Fa:page251#block12">Art. 313-2: 7 years/€750,000 for fraud involving public authority/service, public appeals for funds, vulnerable victims, or social-security bodies; 10 years/€1M for organized-group fraud; 15 years/€1M for organized-group fraud against a public/social-security body.</claim>

### 4.4 Breach of trust (abus de confiance)

<claim cite-ids="Fa:page255#block4..Fa:page255#block9">Article 314-1: diverting, to another's prejudice, funds/valuables/property received subject to a duty to return, account for, or use for a specified purpose. Basic penalty: 5 years, €375,000.</claim> Requires **prior lawful delivery** followed by diversion — distinguishing it from theft (no lawful transfer) and fraud (deceptive inducement of the transfer itself).

**Aggravation:** <claim cite-ids="Fa:page255#block10..Fa:page256#block3">7 years/€750,000 for public fundraising, habitual handling of third-party funds, humanitarian associations, vulnerable victims (art. 314-2)</claim>; <claim cite-ids="Fa:page256#block4..Fa:page256#block8">10 years/€1.5M where committed by a judicial representative or public/ministerial officer (art. 314-3).</claim>

### 4.5 Extortion (extorsion)

<claim cite-ids="Fa:page245#block9..Fa:page245#block14">Article 312-1: obtaining, by violence, threat of violence or coercion, a signature, undertaking, waiver, secret disclosure, or delivery of property. Basic penalty: 7 years, €100,000.</claim> Distinguished from theft (compelled vs. taken) and fraud (coercion vs. deception). <claim cite-ids="Fa:page248#block7..Fa:page248#block14">Blackmail (chantage, art. 312-10) is a related offense using threatened disclosure of reputation-damaging facts rather than violence.</claim>

**Aggravation:** <claim cite-ids="Fa:page245#block15..Fa:page246#block4">10 years/€150,000 for violence (ITT ≤8 days), vulnerable victim, face concealment, school premises (art. 312-2)</claim>; <claim cite-ids="Fa:page246#block5..Fa:page247#block7">up to life imprisonment for organized-group extortion with a weapon (art. 312-2 et seq.).</claim>

### 4.6 Receiving stolen goods (recel)

<claim cite-ids="Fa:page260#block6..Fa:page260#block13">Article 321-1: concealing, possessing, transmitting, or intermediating a thing known to derive from a crime/offense, or knowingly benefiting from its proceeds. Basic penalty: 5 years, €375,000.</claim> The receiver need not be the original thief. <claim cite-ids="Fa:page261#block6..Fa:page261#block9">Sentencing may track the predicate offense's aggravators if the receiver knew them (art. 321-4); recidivism rules assimilate recel to the predicate offense (art. 321-5).</claim>

**Aggravation:** <claim cite-ids="Fa:page260#block14..Fa:page261#block5">Habitual recel, professional-activity recel, or organized-group recel: 10 years/€750,000 (art. 321-2); fine may rise to half the value of the goods.</claim>

### 4.7 Money laundering (blanchiment)

<claim cite-ids="Fa:page280#block7..Fa:page280#block13">Article 324-1: facilitating false justification of the origin of criminal proceeds, or assisting placement/concealment/conversion of such proceeds. Basic penalty: 5 years, €375,000.</claim> Autonomous offense — the launderer need not have committed the predicate offense. <claim cite-ids="Fa:page280#block14..Fa:page281#block2">Article 324-1-1 creates a presumption of criminal origin where the operation's conditions have no justification other than concealment, including specified anonymizing crypto-asset arrangements.</claim>

**Aggravation:** <claim cite-ids="Fa:page281#block3..Fa:page281#block10">Habitual, professional, or organized-group laundering: 10 years/€750,000 (art. 324-2); fine may rise to half the property's value.</claim> *(Last known amendment: art. 324-1, Law No. 2025-532 of 13 June 2025.)*

### 4.8 Destruction/damage to property

<claim cite-ids="Fa:page265#block12..Fa:page265#block17">Article 322-1: destruction, degradation or deterioration of another's property. Basic penalty: 2 years, €30,000 (unless only minor damage).</claim> <claim cite-ids="Fa:page266#block4..Fa:page268#block3">Aggravated to 5 years/€75,000 for multiple perpetrators, vulnerable victim, intimidation of officials, breaking/entering, face concealment, school/agricultural/utility property (art. 322-3); two circumstances → 7 years/€100,000.</claim> <claim cite-ids="Fa:page270#block7..Fa:page271#block7">Damage by explosive, fire, or dangerous means: 10 years/€150,000, up to life imprisonment if death results (art. 322-6).</claim>

### 4.9 Corporate asset misuse (abus de biens sociaux) — Code de commerce

Located in the **Code de commerce** (art. L.241-3 for SARL managers; art. L.242-6 for SA directors/officers), not the Code pénal. <claim cite-ids="7cb1a37e-6763-4ba2-838f-e4d757b51f2c">Requires: use of corporate assets/credit/power/votes, in bad faith, known to be contrary to the company's interest, for personal purposes or to favor another company in which the officer has an interest.</claim> <claim cite-ids="3dc06021-274f-4e8a-8d3d-1fbaea0c5d5a">Personal benefit need not be direct enrichment — securing personal influence/support can suffice where unconnected to the company's interest (15 September 1999 decision).</claim> Traditional penalty: 5 years, €375,000 (verify current text for the applicable company form). <claim cite-ids="63b53f52-b387-4145-abeb-92cb73fec5f7">Limitation may be deferred until the offense becomes apparent and provable, but not on a merely hypothetical date (13 January 1970 decision).</claim>

---

## 5. Business/White-Collar, Corruption, and Cybercrime Offenses

### 5.1 Corruption and influence peddling

| Offense | Article | Actor | Elements | Penalty |
|---|---|---|---|---|
| Passive public corruption / influence peddling | 432-11 | Public official/public-service agent/elected official | <claim cite-ids="_a:page318#block2,_a:page318#block4,_a:page318#block5,_a:page318#block6,_a:page318#block7">Soliciting/accepting an advantage, without right, to perform/omit an official act or to abuse influence over another public decision</claim> | 10 years, €1M (€2M or 2x proceeds if organized group) |
| Active public corruption | 433-1 | Any person | <claim cite-ids="_a:page322#block12,_a:page322#block13,_a:page322#block14,_a:page323#block1,_a:page323#block2,_a:page323#block3,_a:page323#block4">Offering/promising an advantage to an official (or yielding to a solicitation); complete upon the offer/promise, no need for the act to occur</claim> | 10 years, €1M |
| Influence peddling (trafic d'influence) | 433-2 | Intermediary | <claim cite-ids="_a:page323#block6,_a:page323#block7,_a:page323#block8,_a:page323#block9">Soliciting/accepting an advantage to abuse real/supposed influence over a public authority's decision; recipient of influence need not be the one paid</claim> | 5 years, €500,000 |
| Private-sector corruption | 445-1 | Private manager/employee | <claim cite-ids="_a:page375#block4,_a:page375#block5,_a:page375#block6,_a:page375#block7,_a:page375#block8,_a:page375#block9">Advantage to induce an act/omission contrary to legal/contractual/professional obligations</claim> | 5 years, €500,000 |

<claim cite-ids="_a:page318#block3,_a:page318#block8,_a:page318#block9,_a:page318#block10">A cooperation mechanism under art. 432-11 reduces the custodial sentence by two-thirds for an offender who alerts authorities and stops the offense or identifies other participants.</claim> *(Art. 432-11 last amended by Law no. 2020-1672 of 24 December 2020.)*

**Generator distinction:** direct corruption (432-11/433-1) links the advantage to the official's *own* act; influence peddling (433-2) links it to the official's influence over *another* decision-maker.

### 5.2 Illegal taking of interest and favoritism

- **Prise illégale d'intérêts (art. 432-12):** <claim cite-ids="_a:page318#block11,_a:page318#block12,_a:page318#block13,_a:page318#block14,_a:page319#block1">a public official knowingly taking/receiving/retaining an interest that impairs impartiality, independence or objectivity in a matter they supervise/administer. Penalty: 5 years, €500,000.</claim> <claim cite-ids="_e:html25,_e:html26,_e:html27,_e:html28,_e:html29">The Cour de cassation (27 June 2018) held the offense can be consummated even where the official only has an advisory role, not final decision-making power.</claim> *(Last amended by Law no. 2025-1249 of 22 December 2025.)*
- **Favoritisme (art. 432-14):** <claim cite-ids="_a:page320#block9,_a:page320#block10,_a:page320#block11,_a:page321#block1">procuring an unjustified advantage in public contracts by breaching rules guaranteeing free access and equal treatment of candidates. Penalty: 2 years, €200,000.</claim> <claim cite-ids="_c:html15,_c:html16,_c:html19,_c:html20,_c:html21,_c:html22,_c:html23,_c:html24,_c:html25,_c:html26">Upheld against a legality challenge (18 December 2019): the article itself defines the essential offense characteristics.</claim> <claim cite-ids="_d:html45,_d:html46,_d:html48,_d:html49,_d:html50,_d:html52,_d:html53,_d:html55,_d:html56,_d:html57,_d:html58,_d:html59,_d:html60,_d:html61">Applied in the France Télévisions/Bygmalion case to fragmented contracts designed to avoid procurement thresholds (4 March 2020).</claim>

### 5.3 Corporate compliance (Sapin II)

<claim cite-ids="_a:page46#block3,_a:page46#block4,_a:page46#block5,_a:page46#block6,_a:page46#block7,_a:page46#block8,_a:page46#block9,_a:page46#block10,_a:page46#block11,_a:page47#block1,_a:page47#block2">Article 131-39-2 allows a court to impose, for up to 5 years, an anti-corruption compliance program (code of conduct, whistleblowing, risk mapping, due diligence, accounting controls, training, disciplinary regime) supervised by the Agence française anticorruption (AFA).</claim> Introduced by **Law no. 2016-1691 of 9 December 2016 ("Sapin II")**, which also created the **convention judiciaire d'intérêt public (CJIP)** — a negotiated corporate settlement mechanism (CPP art. 41-1-2, not fully researched here — verify separately).

### 5.4 Cybercrime (Code pénal arts. 323-1 to 323-7)

| Offense | Article | Elements | Penalty |
|---|---|---|---|
| Unauthorized access/maintenance in a STAD | 323-1 | <claim cite-ids="_a:page276#block12,_a:page276#block13,_a:page276#block14,_a:page276#block15,_a:page276#block16,_a:page276#block17,_a:page277#block1">Fraudulent access or remaining in an automated data-processing system</claim> | 3 years/€100,000 base; 5 years/€150,000 if data altered; 7 years/€300,000 if a State personal-data system |
| Hindering system operation | 323-2 | <claim cite-ids="_a:page277#block2,_a:page277#block3,_a:page277#block4,_a:page277#block5">Hindering/distorting a STAD's functioning (e.g., denial-of-service)</claim> | 5 years/€150,000 (7 years/€300,000 for State systems) |
| Data manipulation | 323-3 | <claim cite-ids="_a:page277#block6,_a:page277#block7,_a:page277#block8,_a:page277#block9">Fraudulently introducing, extracting, possessing, reproducing, transmitting, deleting or modifying STAD data</claim> | 5 years/€150,000 (7 years/€300,000 for State systems) |
| Possession of hacking tools | 323-3-1 | <claim cite-ids="_a:page277#block10,_a:page277#block11,_a:page277#block12">Importing/possessing/offering tools designed for arts. 323-1 to 323-3 offenses without legitimate reason</claim> | Tracks underlying offense |
| Organized-group cybercrime | 323-4-1 | <claim cite-ids="_a:page278#block5,_a:page278#block6,_a:page278#block7,_a:page278#block8,_a:page278#block9,_a:page278#block10">Organized-group commission of 323-1 to 323-3-1 offenses</claim> | 10 years/€300,000 |

### 5.5 Identity theft and data protection offenses

<claim cite-ids="_a:page208#block6,_a:page208#block7,_a:page208#block8,_a:page208#block9,_a:page208#block10">Article 226-4-1: usurping identity/identifying data to disturb tranquility or harm honor/reputation (includes online conduct). Penalty: 1 year/€15,000; 2 years/€30,000 if by a spouse/partner/cohabitant.</claim>

GDPR/CNIL-linked offenses (Code pénal, each carrying 5 years/€300,000 unless noted):
- <claim cite-ids="_a:page214#block4,_a:page214#block5,_a:page214#block6,_a:page214#block7,_a:page214#block8">Art. 226-16: processing without required formalities, or continuing after a CNIL measure.</claim>
- <claim cite-ids="_a:page215#block2,_a:page215#block3,_a:page215#block4">Art. 226-17: failure to implement GDPR-required security measures.</claim>
- <claim cite-ids="_a:page215#block6,_a:page215#block7,_a:page215#block8,_a:page215#block9">Art. 226-17-1: failure to notify a data breach.</claim>
- <claim cite-ids="_a:page216#block1,_a:page216#block2,_a:page216#block3">Art. 226-18: collecting data by fraudulent/unfair/unlawful means.</claim>
- <claim cite-ids="_a:page216#block7,_a:page216#block8,_a:page216#block9,_a:page216#block10,_a:page216#block11">Art. 226-19: unlawful storage of sensitive data (ethnicity, politics, religion, health, orientation, criminal record).</claim>
- <claim cite-ids="_a:page218#block8,_a:page218#block9,_a:page218#block10,_a:page219#block1,_a:page219#block2">Art. 226-22-2: obstructing CNIL investigations — 1 year/€15,000.</claim>

### 5.6 Insider trading / market abuse

Governed by **Code monétaire et financier, arts. L. 465-1 et seq.** — a dual-track system with an administrative track (AMF sanctions) and a criminal track. The *ne bis in idem* interaction between the two tracks has been shaped by Conseil constitutionnel QPC decisions; **this area requires independent verification** before use, as the current text and holdings were not fully retrieved in this research pass.

---

## 6. Quick-Reference Penalty Ladder (for calibrating case difficulty/stakes)

| Penalty band | Example offenses |
|---|---|
| Fine only / <2 years | Minor harassment, sexual exhibition, failure to notify a data breach obstruction, basic cyber-intrusion |
| 2–5 years | Basic theft, basic property damage, basic sexual assault, favoritisme, basic cybercrime |
| 5–10 years | Fraud, breach of trust, extortion, recel, laundering, corruption, aggravated theft/assault, basic sexual harassment aggravated forms |
| 10–20 years | Aggravated extortion/theft/laundering, rape, aggravated rape, kidnapping |
| 15–30 years / réclusion criminelle | Murder, manslaughter aggravated forms, aggravated kidnapping, rape causing death |
| Life imprisonment | Assassination, aggravated murder, rape with torture, aggravated kidnapping with death |

---

## 7. Difficulty Levers and Scenario-Design Checklist

When generating a case, mix and match:

1. **Qualification ambiguity:** facts that could support two offenses (e.g., theft vs. breach of trust depending on whether possession was lawfully transferred; fraud vs. theft depending on whether the victim "voluntarily" transferred).
2. **Attempt boundary:** facts hovering between mere preparation and "commencement of execution" (Section 1.4) — add a voluntary-abandonment twist vs. an externally interrupted attempt.
3. **Mental-element gradient:** vary between clear intent, deliberate endangerment, "faute caractérisée," and ordinary negligence to test which offense (if any) applies.
4. **Aggravating-circumstance stacking:** combine 1, 2, or 3 statutory aggravators to require the player to correctly total the penalty escalation.
5. **Defense availability:** insert a colorable but ultimately insufficient defense (disproportionate self-defense, avoidable mistake of law, ordinary hardship mis-argued as duress) to test issue-spotting.
6. **Corporate liability layering:** facts implicating both an individual (director/employee) and the company, requiring separate analysis of arts. 121-1/121-2.
7. **Multi-party roles:** author, complicit, and corporate liability in the same fact pattern (e.g., employee commits fraud "for the account of" the company, aided by a colleague).
8. **Procedural posture twist:** an irregular garde à vue extension, a mise en examen without sufficient indices, a prescription (limitation) issue, or a classement sans suite under challenge.
9. **Temporal law-change issue:** facts straddling a legislative amendment, testing non-retroactivity / rétroactivité in mitius (Section 1.2).
10. **Civil party dimension:** add a partie civile with a damages claim distinct from the criminal liability question.

---

## 8. Known Gaps and Verification Needed Before Production Use

- Full current text of CRPC (CPP arts. 495-7–495-16), tribunal de police detailed provisions, and cour criminelle départementale provisions were identified but not read in full.
- Conseil constitutionnel and ECHR case law on legality/foreseeability was not researched.
- Code de la justice pénale des mineurs (juvenile procedure/sentencing) was not researched in detail — needed for any scenario involving a minor defendant.
- Current CMF arts. L.465-1 et seq. (insider dealing) and the administrative/criminal *ne bis in idem* case law were not fully retrieved.
- Code de commerce arts. L.241-3 / L.242-6 (abus de biens sociaux) penalty paragraphs should be verified directly; the offense's existence and elements are well-supported, but the exact current penalty text was not confirmed.
- CPP art. 41-1-2 (CJIP mechanics) was not retrieved in full.
- Many provisions above carry explicit 2022–2026 amendment markers; **re-verify against the live Code pénal / Code de procédure pénale (Légifrance) before relying on a specific article's wording or numeric threshold for a published or graded exercise.**

