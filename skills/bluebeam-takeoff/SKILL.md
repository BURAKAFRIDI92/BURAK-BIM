---
name: bluebeam-takeoff
description: "Quantity takeoff and markup review with Bluebeam Revu PDFs: read markups and measurements, summarise by subject/page/layer, scripts, and UI work with permission."
metadata:
  discipline: "Estimating / QS"
  product: "BURAK BIM 1.3"
---
# Bluebeam Revu - takeoff and markups

1. **Inventory:**
   - `pdf_info path=...` gives pages, sheet sizes and markups per page.
   - `pdf_text` reads title blocks and sheet names, so you can map page numbers to sheet numbers.
2. **Read markups:**
   - `bluebeam_markups path=... [subject, pages, author]` returns subject, page, measurement value and unit, layer, status and custom columns.
   - Ask the user to **save in Revu first**, because the tools read the saved PDF.
3. **Summarise:** `bluebeam_takeoff_summary groupBy=page|layer|author` gives totals per subject (sf/m2, lf/m, cy/m3, ea). Present a table and point out:
   - markups without a measurement;
   - mixed units;
   - duplicate counts.
4. **Check scale:** measurements depend on the page calibration. Compare one known dimension against `pdf_text` or the drawing before trusting the totals.
5. **Batch processing** (Revu eXtreme/Complete): use `bluebeam_script` for Flatten, Stamp, HeaderAndFooter, Import/Export markups and PageRotate.
6. **Drawing new takeoffs or using Revu dialogs:**
   - `bluebeam_open`, then `capture app=bluebeam grid=true`, then the computer-control skill.
   - Use Revu's tool chest and calibrate.
   - Save, then re-read with `bluebeam_markups`.
7. **Deliver:** totals by trade or subject, ready for the estimate or for PlanSwift and Excel.
