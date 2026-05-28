// Observe the sign-in form. When it is on screen, hide the hero "Sign in" jump button
// (the form is already visible, so no jump is needed). When it is off screen — narrow
// viewports, zoomed in, or any layout that pushes the form below the fold — reveal the
// button so the user can scroll to it. Returns a dispose handle for component teardown.

const HIDDEN_CLASS = "sign-in-jump-button--hidden";

export function setup(buttonId, targetId) {
  const button = document.getElementById(buttonId);
  const target = document.getElementById(targetId);
  if (!button || !target) {
    return { dispose() {} };
  }

  const clickHandler = (event) => {
    event.preventDefault();
    target.scrollIntoView({ behavior: "smooth", block: "start" });
  };
  button.addEventListener("click", clickHandler);

  let observer = null;
  if (typeof IntersectionObserver === "function") {
    observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        button.classList.toggle(HIDDEN_CLASS, entry.isIntersecting);
      }
    }, { threshold: 0.25 });
    observer.observe(target);
  } else {
    // No IntersectionObserver — leave the button visible as a safe default.
    button.classList.remove(HIDDEN_CLASS);
  }

  return {
    dispose() {
      button.removeEventListener("click", clickHandler);
      observer?.disconnect();
    }
  };
}
