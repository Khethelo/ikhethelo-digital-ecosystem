import { describe, expect, it } from "vitest";
import { TestBed } from "@angular/core/testing";
import { FormControl, FormGroup } from "@angular/forms";
import { Field } from "./ui";
import { numeric } from "../core/forms";
describe("numeric field value accessor", () => {
  it("keeps entered values numeric, blank values null, and reports precision instead of coercing strings", () => {
    const fixture = TestBed.createComponent(Field);
    const control = new FormControl<number | null>(null, numeric(100, 2));
    fixture.componentRef.setInput("form", new FormGroup({ amount: control }));
    fixture.componentRef.setInput("field", {
      key: "amount",
      label: "Amount",
      type: "number",
      step: "0.01",
    });
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector(
      "input",
    ) as HTMLInputElement;
    input.value = "12.34";
    input.dispatchEvent(new Event("input"));
    expect(control.value).toBe(12.34);
    expect(control.valid).toBe(true);
    input.value = "12.345";
    input.dispatchEvent(new Event("input"));
    expect(control.errors?.["precision"]).toEqual({ places: 2 });
    input.value = "";
    input.dispatchEvent(new Event("input"));
    expect(control.value).toBeNull();
  });
});
