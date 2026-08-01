import AppRoutes from "./routes/AppRoutes";
import { ThemeManager } from "./theme";

/** AI22.7B — ThemeManager wraps the app; no page redesign. */
function App() {
  return (
    <ThemeManager>
      <AppRoutes />
    </ThemeManager>
  );
}

export default App;