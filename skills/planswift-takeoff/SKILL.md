---
name: planswift-takeoff
description: "Quantity takeoff and estimating in PlanSwift through BURAK BIM: job tree, takeoff items, quantities, properties/formulas, new items, reports."
metadata:
  discipline: "Estimating / QS"
  product: "BURAK BIM 1.3"
---
# PlanSwift - takeoff and estimating

1. **Connect:** `planswift_status`. PlanSwift must be running with the job open.
2. **Explore:** `planswift_tree path="\Job" depth=2`, then go deeper where you need to (pages, takeoff folders, estimating).
3. **Quantities:** `planswift_quantities` gives a flat list of items with Qty, Area, Linear Total, Volume, Count and Price Total. Group the result by folder or trade.
4. **Item detail:** `planswift_item path=...` shows every property with its result and formula.
5. **Adjust the estimate after approval:** `planswift_set` sets values or formulas such as Height, Waste %, Price Each and Labor.
6. **Create items:**
   - `planswift_new_item parent="\Job\Takeoff" type=Area|Linear|Count|Segment name=...`.
   - Draw the takeoff on the page with `capture app=planswift grid=true` and the computer-control skill.
7. **Advanced:** use `planswift_call` for any API method (for example GetJobTotal).
8. **Verify:** re-read the quantities after any change. Compare the job total before and after, and report the difference.
