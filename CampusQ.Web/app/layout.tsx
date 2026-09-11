import type { Metadata } from "next";
require("./globals.css");
import { AuthProvider } from "../lib/auth";
import Header from "../components/Header";

export const metadata: Metadata = {
  title: "CampusQ",
  description: "CampusQ Queue Management System",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>
        <AuthProvider>
          <Header />
          {children}
        </AuthProvider>
      </body>
    </html>
  );
}