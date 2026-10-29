# Claude Design brief — ARC review refinements (031)

**Product**: Neko HOA, board and resident web app (Angular). **Surfaces**: Board ▸ Architectural applications, Board ▸ Community Home "Needs your vote" card, and Resident ▸ My architectural requests ▸ request page.
**What exists**: a working implementation built from the existing tokens in `neko-hoa/src/styles.scss` (soft pastel "paper & ink" style: `--paper #fdfcfd`, `--ink #3a2f3f`, `--pink`, `--rose`, `--lav`, `--lav-2`, `--violet`, `--ok`/`--ok-bg`, `--warn`/`--warn-bg`, pill buttons with a 1.5px ink border, dashed table separators, hand-lettered accent word in page titles). Storybook stories `Board/Architectural/DetailPanel` (Loading, VotedCanUndo, ApprovedByDefault, WithHomeownerComments) show the current states.
**Ask**: high-fidelity designs for the states below that match the existing style. Desktop first (board is desktop-only), with a phone layout for the resident page. WCAG 2.1 AA; nothing may rely on color alone.

## 1. Vote choice toggles (pick, then Submit or Save)

Three options: **Approve**, **Request revisions**, **Deny**. Nothing is selected at first. The options need these states:
- default, **hover** (currently a pink tint with a rose border; it should read as "clickable" and fit the palette), focus-visible, selected (only one at a time), and disabled while saving.
- Once one is picked, a primary **Submit** button appears (labelled **Save** on the Community Home card, **Submit vote** in the detail card). Show the layout before and after a pick, and make sure the row doesn't jump when the button appears.
- Sizes: compact (table cell about 300px wide, also holds a dashed **Ask question** button) and regular (the "Cast your vote" card).

## 2. Applications table — new "Final decision" column

The last column, after "Your vote", about 92px wide. Its states:
- **undo** text link and a muted "until 10/12/26 3:05 PM" under it, while the viewer's vote can still change.
- final marks: **✓ Approved** (green), **✕ Denied** (red), **↻ Revisions requested** (yellow), **– Withdrawn** (grey). Each is a 22px circle with a tooltip and text label.
- muted "pending" while someone else's vote can still change the result.
- Rows are clickable: design a row hover and a selected-row state (the selected row currently has a violet outline).

## 3. Detail panel

Two cards side by side: Details (left) and Vote (right), with a full-width **Homeowner comments** card below.
- **Loading skeletons** for both cards, shown at once when a row is clicked. Use the shimmer bars for title, fields, the file list and the button row.
- **Details card**: the vote list reads "Aaliyah Brooks — voted approve" (with the comment, if any), followed by a **Board final result:** line with the final mark and text (Not yet decided / Application Approved / Revisions Requested (effectively denied; the resident is asked to revise and resubmit) / Denied / Approved by default). While votes can still change, add a muted line: "Pending — board members can change their votes until …".
- **Info-requested marker**: pill, the message, sender and date, then the notice "Info requests do not delay the due date of the board vote result. Failure to approve or deny by the due date may result in auto-approval. To request revisions, please select "Request revisions"." It must stay readable without becoming an alarm.
- **Vote card**:
  - (a) Can vote: comment box (with **hover** and focus states), the choices, Submit vote, and Ask question.
  - (b) Already voted: "you voted approve" pill, an **undo** link, and "You can undo and recast your vote until …".
  - (c) Recused.
  - (d) **Approved by default**: a green card with the ✓ mark, the title "Approved by default" and the statement citing the governing documents, e.g. "…Under Article IX, Section 9.4 of the Declaration of Covenants, Conditions and Restrictions, an application the board does not act on within the 30-day review period is deemed approved." Also design the denied-by-default variant.
- **Homeowner comments** (board view, read-only): a thread of entries with author, date and time, text, and file chips that open in a new tab. Include the empty state "The homeowner hasn't commented on this application."

## 4. Community Home — "Needs your vote" card

Each row holds the ID link, project, address · owner, file count, tally, due date and the choice toggles + **Save**. The whole row is clickable and opens the application. Design the row hover so it's distinct from the option hover.

## 5. Resident request page

- **Header**: title, "Back to my requests", and a red **Withdraw request** button at the top right. Show the danger style and its hover; it opens the existing confirm dialog.
- **"Comments to the board"** card: the thread of my comments (date and time, text, file chips with Open), the empty state "No comments yet.", and a composer with a textarea (placeholder "e.g. I lowered the fence to 5ft — revised plan attached."), a 0/2000 counter, a file picker (up to 5 files; list picked files with Remove), an error line, and **Post comment** (disabled until there is text or a file, with a spinner while posting). Show it below the decision card on a "Denied · revisions requested" request, which is the main use: the owner replies with revised plans.

## Deliverables

Annotated frames for each state above (light theme), the hover, focus and disabled specs for the new controls, and the token values used. Keep to existing tokens where possible; flag any new token, such as the yellow used for ↻.
